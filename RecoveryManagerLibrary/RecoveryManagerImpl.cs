using PwdRecover;       //for PwdRecover.dll
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;




namespace RecoveryManagerLibrary
{
    /// <summary>
    /// Class that is the implemented recovery manager. Handles all connections to the services and the presentation tier (GUI and Web services).
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    public class RecoveryManagerImpl : IRecoveryManager
    {

        private PWRecoveryJob currentJob;                                           //The job currently doing
        private char[] defaultCharSet = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };   //now I know my abc

        private List<IRecoveryManagerServiceCallback> servicesList;                 //List of all recovServices
        private Dictionary<int, IRecoveryManagerServiceCallback> servicesMap;       //Map by id of all services.
        

        private Queue<IRecoveryManagerServiceCallback> idleServices;                //List of idle recovery services
        private static readonly Object servicesLock = new Object();                 //Lock on the idle services queue and containers..
        private static Semaphore webServiceSemaphore;                               //Used to stop beginViaWeb() from finishing until password found. That way the ajax function returns at the right time. 
        

        private IRecoveryManagerGUICallback guiClient;                              //The gui client. NOTE: different callback interface 

        private NetTcpBinding tcpBinding;                                           //The tcp binding object
        private string sURL = "net.tcp://localhost:5002/RecovManager";              //URL to find this manager

        private char[] testCharSet = { 'A', 'B', 'C' };                             //Used for debugging and design.
        private char[] charSet;                                                     //The char set used. 

        private int nextServiceID;                                                  //The ID to assign to a service if it connects.
        private List<PWBatch> assignedBatches;                                      //List of assigned batches
        private Boolean cleanupBegun = false;                                       //Clean up process flag. See beginCleanup
        
        
        private long approxBatchTimeMS = 10 * 1000;
        private long maxBatchTimeMS;        //Set to twice the existing batch time plus standard 2x 60 second timeout. 

        /// <summary>
        ///             Constructor for the manager.
        /// </summary>
        public RecoveryManagerImpl()
        {
            Console.WriteLine("*************************************");
            Console.WriteLine("Distributed Password Recovery Manager");
            this.guiClient = null;
            servicesList = new List<IRecoveryManagerServiceCallback>();
            servicesMap = new Dictionary<int, IRecoveryManagerServiceCallback>();
            idleServices = new Queue<IRecoveryManagerServiceCallback>();

            assignedBatches = new List<PWBatch>();

            maxBatchTimeMS = 2 * approxBatchTimeMS + 2 * 60 * 1000;     //Max time for a batch to complete. If too small, will cause big problems!
        }

        /********************************************************************************************************************************************************************/
        /************************************************ IRecoveryGUI Interface Methods ************************************************************************************/
        /********************************************************************************************************************************************************************/
        
        /// <summary>
        ///             generates a new password of given length, and creates a new job based on this.
        /// </summary>
        /// <param name="length">   Length of the password to be generated, between 1 and 10 inclusive </param>
        /// <returns>   The encrypted password. If job already in progress, it returns existing jobs encrypted password </returns>
        public string generateNewPassword(int length)
        {
            String encryptedPW = null;
            if (length > 0 && length <= 10)
            {
                if (currentJob == null)
                {
                    Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^ 1");
                    PasswordServices p = new PasswordServices();
                    encryptedPW = p.NextEncryptedPasswordToRecover(length);
                    this.currentJob = new PWRecoveryJob("1", encryptedPW, defaultCharSet, length);
                }
                else if (currentJob != null && currentJob.passwordFound())
                {
                    Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^ 2");
                    PasswordServices p = new PasswordServices();
                    encryptedPW = p.NextEncryptedPasswordToRecover(length);
                    this.currentJob = new PWRecoveryJob("1", encryptedPW, defaultCharSet, length);
                }
                else if (currentJob != null && !currentJob.jobInProgress())      //Then the job was set, but never started, so GUI is overwriting the job before starting.
                {
                    Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^ 3");
                    PasswordServices p = new PasswordServices();
                    encryptedPW = p.NextEncryptedPasswordToRecover(length);
                    this.currentJob = new PWRecoveryJob("1", encryptedPW, defaultCharSet, length);
                }
                else
                {
                    Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^ 4");
                }

            }
            return encryptedPW;
        }

        /// <summary>
        ///             Function to kickstart the program if connected via the web. 
        /// </summary>
        /// <returns>   The password </returns>
        /// <remarks>   The web function call needs to hold off returning until the password is found. So a semaphore is used to put a hold in it.
        ///             The semaphore is released when the password is found (ie method batchComplete(...) </remarks>
        public String beginViaWeb()
        {
            String password = "Error: web service already in use by another browswer. One service at a time.";
            if (webServiceSemaphore == null)
            {
                password = "";
                webServiceSemaphore = new Semaphore(1, 1);  //Initial count of 1, max count of 1;
                webServiceSemaphore.WaitOne();              //Change semaphore by 1 and go (wont block). Note this implementation reduces web clients to one at a time!!
                begin();                                    //Begin the recovery process.

                webServiceSemaphore.WaitOne();              //Block until passwordFound() releases semaphore.


                password = currentJob.getRecoveredPassword();
                webServiceSemaphore = null;                 //Reset so new request can work.
            }
            
            return password;
        }


        /// <summary>
        ///             Kickstarter function to begin the password recovery process.
        /// </summary>
        /// <remarks>   As workers beome free after initial assignment, they will be automatically assigned a new batch in their batchComplete(..) function. </remarks>
        public void begin()
        {
            /*if (currentJob!=null && currentJob.jobInProgress())
            {
                //TODO: cancel job.
                //TODO: no restarting the job!!
            }
            else */
            if (currentJob != null)
            {
                Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
                Console.WriteLine("NEW JOB STARTED. ");
                currentJob.setBatchSize(servicesMap.Count);
                currentJob.printJob();

                Console.WriteLine("Total services available: " + servicesList.Count);
                lock (servicesLock)             //Lock the idleServices Queue, because don't new services adding themselves as we dequeue.
                {
                    if (idleServices.Count > 0 && guiClient != null)
                    {
                        System.Threading.Thread statsUpdateThread = new Thread(beginStatsUpdate);                  //Begin the update tot he GUI
                        statsUpdateThread.Start();
                    }
                    while (idleServices.Count > 0 && currentJob.availableBatches())                     //While we got some free workers, and while we got batches available.
                    {

                        IRecoveryManagerServiceCallback service = idleServices.Dequeue();
                        if (service == null)
                        {
                            Console.WriteLine("Null found in idle queue. Service reference ignored!");     //what else can I do?
                        }
                        else if (servicesList.Contains(service))                                      //Check if in the list of connected services. (An old ref could be in the que)
                        {


                            PWBatch batch = currentJob.getNextBatch();
                            if (batch != null)
                            {
                                
                                assignBatchToService(service, batch);
                                
                            }
                            else
                            {
                                Console.WriteLine("Begin: no more batches to assign");
                            }
                        }
                    }
                }
                Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
            }
        }


        /// <summary>
        ///             Registers the GUI client.
        /// </summary>
        /// <returns>   True if successful, False if a GUI already connected. </returns>
        public Boolean addGUIClient()
        {
            Boolean outcome = false;
            if (this.guiClient == null)
            {
                IRecoveryManagerGUICallback client = OperationContext.Current.GetCallbackChannel<IRecoveryManagerGUICallback>();    
                this.guiClient = client;
                Console.WriteLine("The GUI client has connected.");
                outcome = true;
            }

            return outcome;
        }

        /// <summary>
        ///             De-registers the GUI client.
        /// </summary>
        public void GUIDisconnect()
        {
            this.guiClient = null;
            Console.WriteLine("GUI Client disconnected");
        }


        /// <summary>
        ///             Finds whether a job is in progress
        /// </summary>
        /// <returns>   False if job complete or not job ever created. </returns>
        public Boolean jobInProgress()
        {
            Boolean outcome = false;
            if (currentJob != null && currentJob.jobInProgress())
            {
                outcome = true;
            }
            return outcome;
        }

        /// <summary>
        ///             Bonus functionality: allows a user to enter a custom password to encrypt and try crack.
        /// </summary>
        /// <param name="customPW">     The plaintext password to encrypt and try crack. </param>
        /// <returns>   True if it was a valid password ie contains only valid characters from default char set 0-9, A-Z, a-z </returns>
        public Boolean setCustomPassword(String customPW, ref String encryptedPW)
        {
            Boolean outcome = false;

            if (validateCustomPW(customPW))
            {
                outcome = true;
                PasswordServices p = new PasswordServices();
                encryptedPW = p.EncryptString(customPW);

                this.currentJob = new PWRecoveryJob("1", encryptedPW, defaultCharSet, customPW.Length);
                Console.WriteLine("CUSTOM PASSWORD: The encrpted PW is: " + encryptedPW);
            }
            return outcome;
        }

        /// <summary>
        ///         Cancels the job at GUI / users request.
        /// </summary>
        public void cancelJob()
        {
            currentJob.endJob();
            currentJob = null;
            lock (idleServices)
            {
                assignedBatches = new List<PWBatch>();
            }

        }


        /********************************************************************************************************************************************************************/
        /************************************************ IRecoveryService Interface Methods ********************************************************************************/
        /********************************************************************************************************************************************************************/


        //Synchronise both the registering and de-regisering in one method. Avoids racey conditions. 
        /// <summary>
        ///             Allows a recovery service to register or de-register at the manager.
        /// </summary>
        /// <param name="isRegistering">    Indicates whether registering or deregistering. </param>
        public void registerService(Boolean isRegistering)
        {
            IRecoveryManagerServiceCallback remoteRecoveryService = OperationContext.Current.GetCallbackChannel<IRecoveryManagerServiceCallback>();
            lock (servicesLock)
            {
                if (isRegistering)
                {

                    if (!servicesList.Contains(remoteRecoveryService))
                    {
                        servicesList.Add(remoteRecoveryService);                    //add to total list
                        remoteRecoveryService.setServiceID(nextServiceID);          //Set ID
                        servicesMap.Add(nextServiceID, remoteRecoveryService);      //Add to map (maps ID to Server reference)
                        nextServiceID++;
                        idleServices.Enqueue(remoteRecoveryService);                //add to idleServices
                        Console.WriteLine("A recovery service has been added. Total = " + servicesList.Count);
                        if (guiClient != null)
                        {
                            guiClient.setNumConnected(servicesList.Count);  //exceptions. TODO
                        }
                    }
                    else
                    {
                        throw new FaultException("Service tried to register but was already registered");
                    }
                }
                else
                {
                    servicesList.Remove(remoteRecoveryService);        //remove from total list of jobs.
                    if (guiClient != null)
                    {
                        guiClient.setNumConnected(servicesList.Count);  //exceptions. TODO
                    }
                    //No need to remove from the idleServices queue because can't remove out of order from queue
                    //To deal with this, the reference remains in the queue. When dequeued, code will check is in the recovServices list
                    //before assigning it. See begin() and finishedBatch(...)
                }
            }

        }



        /// <summary>
        ///             Called by a service when its batch is complete. 
        /// </summary>
        /// <param name="batch">        Struct containing the batch info, including start and end permutation numbers. </param>
        /// <param name="serviceID">    ID of the service that has called this method remotely </param>
        /// <param name="wantMore">     Flags whether the service wants more batches. </param>
        /// <param name="pwFound">      Flags whether the service found the password. </param>
        /// <param name="password">     The password, if found. </param>
        /// <remarks>   When a service completes a batch, it calls this method.
        ///             Here, the service is assigned a new batch if it has flagged that it wants more. This avoid polling
        ///             the idle services que which is wasteful. 
        ///             This method will notify the GUI client if the password is found.
        ///             Alternatively, if the user is a web client, it will release the semaphore that is stopping the return
        ///             of the password to the web service. 
        ///             When all batches have been assigned, this method will kickstart a cleanup method which deals with
        ///             batches that were assigned but didnt complete in an allowable time. See beginCleanup() 
        /// </remarks>
        public void batchComplete(Batch batch, int serviceID, Boolean wantMore, Boolean pwFound, String password)
        {

            long startPermNum = batch.startPermNumber;                  
            long endPermNum = batch.endPermNumber;
            try
            {
                currentJob.batchCompleted(startPermNum, endPermNum);
            }
            catch(NullReferenceException e)
            {
                //Ignore: job got cancelled, so no point in marking as complete.
            }
            
            Console.Write("Finished batch from " + startPermNum + " to " + endPermNum + ".");
            
            if (pwFound)
            {
                //TODO: verify result!
                Console.WriteLine("Found password: " + password);
                Console.WriteLine("ID of finder: " + serviceID);
                currentJob.endJob();
                currentJob.setRecoveredPassword(password);
                if (guiClient != null)
                {
                    try
                    {
                        guiClient.passwordFound(password);
                    }
                    catch (CommunicationException e)
                    {
                        Console.WriteLine("Error: tried to alert GUI client that password was found, but failed:\n" + e.Message);
                        guiClient = null;       //TODO: bother with this?
                    }
                    catch (TimeoutException te)
                    {
                        Console.WriteLine("Error: could not alert GUI client that password found due to timeout" + te.Message);             //Note that this timeout won't stop other services from marking theirs as complete (method is not synchronised).
                        guiClient = null;
                    }
                }
                if(webServiceSemaphore != null)
                {
                    webServiceSemaphore.Release();          //The web service method will now complete & return the value. (Before this, it was blocking.)
                }

                poisonBatches(batch.jobID);                 //Tells services to stop their batches. 
            }

            PWBatch nextBatch = null;
            IRecoveryManagerServiceCallback remoteRecoveryService;
            
            //Next, lock just long enough to modify various service containers (servicesList etc) and get the next password batch. (Needs to be synchronised).
            //UNlock just BEFORE giving trying to assign the batch, otherwise the passing of the new batch could timeout and slow ALL other batches down.
            //Note: no chance of deadlock as there is only one lock in the program! (semaphore lock does not intersect with services.)
            lock (servicesLock)                 //only assign one batch at a time!! Or the work could be duplicated!
            {
                assignedBatches.Remove(new PWBatch(batch));     //Equals method used in remove will match on start, end and jobID. (Doesn't need to be same object reference)

                //Get the service from the id to service map (***Can't do operation context, because it will return the proxy, not the service!!!***)
                Boolean isInMap = servicesMap.TryGetValue(serviceID, out remoteRecoveryService);    //if returns false, then somehow been given a bad serviceID! Could be malicous activity.
                

                if(wantMore && isInMap && currentJob != null && currentJob.availableBatches())
                {
                    nextBatch = currentJob.getNextBatch();
                    
                }
                else if(wantMore && isInMap && currentJob != null && !currentJob.availableBatches())
                {
                    idleServices.Enqueue(remoteRecoveryService);
                }
                else if(wantMore && currentJob == null)
                {
                    idleServices.Enqueue(remoteRecoveryService);
                }
                else if(!wantMore)
                {
                    servicesList.Remove(remoteRecoveryService);
                    servicesMap.Remove(serviceID);    
                }

                //If no more jobs are available, then we need to check the list of assigned to see if the batch is long overdue (then the service quit without notifying the manager).
                if(currentJob != null && !currentJob.availableBatches() && !cleanupBegun && !pwFound && nextBatch == null)     
                {
                    System.Threading.Thread thread = new Thread(beginAssignedBatchesCleanup);
                    cleanupBegun = true;
                    Console.WriteLine("All batches have been assigned! Server will now assess the assigned batches for re-assignement (in case they have taken too long");
                    thread.Start();
                }
            }

            //Finally, we pass the batch across the network to the service! (Had to do it here, or timeouts would slow them all down)
            if(wantMore && nextBatch != null)
            {
                assignBatchToService(remoteRecoveryService, nextBatch);
            }   
        }


        /********************************************************************************************************************************************************************/
        /******************************************************************** Other Functions *******************************************************************************/
        /********************************************************************************************************************************************************************/

        /// <summary>
        /// Assigns a given batch to a service.
        /// </summary>
        /// <param name="service"> The recovery service that crunches passwords </param>
        /// <param name="nextBatch"> The batch to assign it to </param>
        /// <remarks> This is typically done in its own thread. Eg, when a batch is complete, the service is assigned a new one on that method.</remarks>
        private void assignBatchToService(IRecoveryManagerServiceCallback service, PWBatch nextBatch)
        {
            if (service == null)
            {
                Console.WriteLine("\nERROR: tried to assign a batch, but service was null");
            }
            else if (nextBatch != null)
            {

                lock(idleServices)
                {
                    assignedBatches.Add(nextBatch);
                }

                try
                {
                    //int serviceID = service.getServiceID();
                    service.assignBatch(nextBatch.getBatch());             //can throw communicationException. TODO.
                    Console.WriteLine("Assigned batch with permutation range " + nextBatch.getBatch().startPermNumber + " to " + nextBatch.getBatch().endPermNumber );
                }
                catch (TimeoutException te)
                {
                    Console.WriteLine("Error: could not assign batch to service due to timeout:\n" + te.Message);
                }
                catch(CommunicationException ce)
                {
                    Console.WriteLine("Error: could not assign batch to service due:\n" + ce.Message);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error: unexpected exception occurred:\n" + e.Message);
                }

            }


            //TODO: throw exception if times out? Remove from serviceList?

        }

        private void poisonBatches(String jobID)
        {
            lock (idleServices)     //Can't iterate assignedBatches and allow it to change from somewhere else.
            {
                foreach (PWBatch b in assignedBatches)
                {
                    if (b.getBatch().jobID.Equals(jobID))
                    {
                        IRecoveryManagerServiceCallback service = b.getAssignedService();
                        if (service != null)
                        {
                            try
                            {
                                service.setPoisonPill();
                            }
                            catch (CommunicationException e)
                            {
                                Console.WriteLine("Error: could not poison service due to comunication error:\n" + e.Message);
                            }
                            catch (TimeoutException e)
                            {
                                try
                                {
                                    Console.WriteLine("Error: could not poison service due to timeout. Trying 2nd time...");
                                    service.setPoisonPill();
                                }
                                catch (CommunicationException e2)
                                {
                                    Console.WriteLine("Error: could not poison service due ot communication error:\n" + e2.Message);
                                }
                                catch (TimeoutException e2)
                                {
                                    Console.WriteLine("Error: could not poison service upon second attempt. Giving up on this one.");
                                }
                                catch (Exception e2)
                                {
                                    Console.WriteLine("Error: unexpected exception occurred when trying to poison service second time round:\n" + e2.Message);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error: could not poison service: unexpected exception occurred:\n" + e.Message);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///             Method to check list of all assigned batches and to re-assign them if they are deemed to take too long.
        /// </summary>
        /// <remarks>   This is spun off in a new thread by batchComplete(...), and will loop until all batches are complete or the password is found. </remarks>
        private void beginAssignedBatchesCleanup()
        {
            //Console.WriteLine("Cleanup Begun. Number of batches to check for taking too long = " + assignedBatches.Count);
            while(assignedBatches.Count > 0 && currentJob != null && !currentJob.passwordFound())
            {
                Console.WriteLine("Cleanup: number of assigned batches whose service could have failed: = " + assignedBatches.Count +"\n Finding oldest:");
                //1. Get the oldest batch.
                long oldestBatchTime = 0;
                PWBatch oldestBatch = null;

                lock(idleServices)
                {
                    foreach(PWBatch b in assignedBatches)
                    {
                        long batchAge = b.ageInMilliseconds();
                    
                        if(batchAge > oldestBatchTime)
                        {
                            oldestBatchTime = batchAge;
                            oldestBatch = b;
                        }
                    }
                    assignedBatches.Remove(oldestBatch);
                }
                

                //2: see if its taken too long:
                if (oldestBatchTime > maxBatchTimeMS)
                {
                    IRecoveryManagerServiceCallback service = null;
                    int serviceID = -1;
                    lock(idleServices)
                    {
                        if(idleServices.Count > 0)
                        {
                            service = idleServices.Dequeue();               //Get a new service to do the job.  //TODO: can't be sure empty que!
                        }
                            
                        if(service != null)
                        {
                            oldestBatch.restartAge();
                        }
                    }
                    if(service != null && !currentJob.passwordFound())      //Don't re-assign if the pw has been found!
                    {
                        Console.Write("Cleanup: Found oldest batch - ");
                        assignBatchToService(service, oldestBatch);
                    }

                }
                else
                {
                    long sleepTime = maxBatchTimeMS - oldestBatchTime + 500;    //Time to sleep thread if all batches were still young enough to be completing. Add half a second to be sure.
                    if(sleepTime > 0)
                    {
                        Console.WriteLine("All batches are still being processed within allowable time of " + maxBatchTimeMS/1000 + " seconds. Waiting " + sleepTime / 1000 + " seconds before finding oldest batch to test again.");
                        Thread.Sleep((int)sleepTime);
                    }
                }
            }
            cleanupBegun = false;
            Console.WriteLine("Clean up finished! All batches have been completed!");
        }

        /// <summary>
        ///             Begins status updates to the GUI client if it is connected.
        ///             Must be called on a new thread.
        /// </summary>
        private void beginStatsUpdate()
        {
            Boolean GUIConnected = true;
            int timeouts = 0;
            while(currentJob != null && !currentJob.passwordFound() && GUIConnected)
            {
                if(guiClient!= null)
                {
                    try
                    {
                        int numConnected = servicesList.Count;
                        long numTested = currentJob.getNumTested();
                        long rate = currentJob.getRate();
                        long elapsedSeconds = currentJob.getElapsedSconds();
                        //Console.WriteLine("JOB STATS: numConnected = " + numConnected + ", numTested = " + numTested + ", rate =" + rate + ", seconds = " + elapsedSeconds);
                        guiClient.updateJobStats(servicesList.Count, numTested, rate, elapsedSeconds);      //update stats first.

                        Thread.Sleep(1000);             //Sleep for a second. Aim is to update very second.
                    }
                    catch (NullReferenceException e)
                    {
                        //Then the GUI client d/c just after checking it was null
                        GUIConnected = false;
                        guiClient = null;
                    }
                    catch (CommunicationException e)
                    {
                        GUIConnected = false;
                        guiClient = null;
                        Console.WriteLine("Error: could not update stats to GUI due to communication error:\n" + e.Message);
                        Console.WriteLine("GUI is assumed to be disconnected. Please relaunch the GUI.");
                    }
                    catch(TimeoutException e)
                    {
                        Console.WriteLine("Error: could not update stats to GUI due to timout error:\n" + e.Message);
                       
                        timeouts++;
                        if(timeouts >= 2)
                        {
                            GUIConnected = false;
                            guiClient = null;
                            Console.WriteLine("GUI stats update: Second timout failed: the GUI is assumed to be d/c:\n" + e.Message);
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("ERROR: unexpected error occurred when updating GUI stats:\n" + e.Message);
                    }
                }
            }
        }

        /// <summary>
        ///             Method to ping.
        /// </summary>
        /// <returns>   The pong message </returns>
        public string ping()
        {
            return "pong from manager";
        }

        

        /// <summary>
        ///             Validates a custom made password to see it contains only default char set chars: 0-9, A-Z, a-z
        /// </summary>
        /// <param name="customPW">     The plaintext password to validate. </param>
        /// <returns>   True if made up of only allowable chars, false otherwise. </returns>
        public Boolean validateCustomPW(String customPW)
        {
            Boolean outcome = false;
            String regex = "^[a-zA-Z0-9_]*$";
            if (System.Text.RegularExpressions.Regex.IsMatch(customPW, regex))
            {
                outcome = true;
            }

            return outcome;

        }

        

        /// <summary>
        ///             Gets the job stats and passes them to caller
        /// </summary>
        /// <param name="numConnected">     Number of connected recovey services. </param>
        /// <param name="numTested">        Number of permutations tested so far. </param>
        /// <param name="rate">             APPROX rate in permutations per second tested </param>
        /// <param name="elapsedSeconds">   Time in seconds since job begun. </param>
        /// <remarks>   This method was used in an older Service Oriented approach, but since it was not in line with assignment specs, 
        ///             its been replaced with a callback approach.
        /// </remarks>
        public void getJobStats(out int numConnected, out long numTested, out long rate, out long elapsedSeconds)
        {
            numConnected = servicesList.Count;
            numTested = 0;
            rate = 0;
            elapsedSeconds = 0;
            if (currentJob != null)
            {
                numTested = currentJob.getNumTested();
                rate = currentJob.getRate();
                elapsedSeconds = currentJob.getElapsedSconds();
            }
        }

       
    }
}
