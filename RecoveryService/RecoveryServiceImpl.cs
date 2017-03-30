using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using PwdRecover;
using System.Runtime.Remoting.Messaging;
using RecoveryManagerLibrary;

namespace RecoveryService
{
    //Delegate for doing the work of a batch. Used by assingBatch()
    public delegate void BatchDelegate(ref Batch batch, out Boolean wantMore, out Boolean pwFound, out String password);
   
    //TODO: double check this.
    [ServiceBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
    internal class RecoveryServiceImpl : IRecoveryManagerServiceCallback
    {
        //Class fields:
        private int serviceID;                                                          //ID of this service, assigned by RecoveryManager server.
        private NetTcpBinding tcpBinding;                                   
        private String recovManagerURL = "net.tcp://localhost:5002/RecovManager";       //URL of the manager.

        private ChannelFactory<IRecoveryManagerService> rmFactory;  
        private IRecoveryManagerService manager;                                        //The manager reference. Object is remote.

        private int pwLength;                   //Length of password
        private char[] charSet;                 //charSet of the password. Must be 0-9 A-Z and a-z for the current PwdRecover.dll. But I left it in for better extendability!
        private String jobID;                   //Not really utilised, but left in for extendability.

        private Boolean wantMore;               //A flag to say whether this service wants more. Assumes true in this version. In future versions, will allow user to decline futher batches.
        private Boolean poisonPill;

        private PasswordServices pwdEncrypter;            //The .dll reference object, that does the SHA-1 encryption

        public RecoveryServiceImpl()
        {

            tcpBinding = new NetTcpBinding();                           //The tcp binding
            IRecoveryManagerServiceCallback callBackObject = this;      //A reference to self, required in duplex channels / callback connections.

            try
            {
                pwdEncrypter = new PasswordServices();                            //Test the PwdRecover.dll is actually there!!
                pwdEncrypter.NextEncryptedPasswordToRecover(3);


                rmFactory = new DuplexChannelFactory<IRecoveryManagerService>(callBackObject, tcpBinding, recovManagerURL);     //Sets up the channel factory with above created tcp binding and url. 
                manager = rmFactory.CreateChannel();                    //Creates the channel for client-server communication.   
                

                manager.registerService(true);                          //true = isRegistering. 

                Console.WriteLine("This RecoveryService has connected to RecoveryManager Server!");
                this.wantMore = true;   //assume always want more work / batches..
                this.poisonPill = false;

            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine("Error: PwdRecover.dll not found. Service aborted.");
            }
            catch(BadImageFormatException e)
            {
                Console.WriteLine("Error: PwdRecover.dll is the wrong format. If you're running 32 Bit machine, use the 32 Bit .dll!");
            }
            catch (ArgumentNullException)
            {
                //Shouldn't happen, since tcpBinding and sURL are guarenteed to be created
                //Here for completeness.
            }
            catch (CommunicationException ce)
            {
                Console.WriteLine("***ERROR***: Could not access the Manager server. It appears to be down! Message:" + ce.Message);

            }
            catch (TimeoutException te)
            {
                Console.WriteLine("***WARNING***: Server timed out - could not initialise connection to Manager server");
                Console.WriteLine("Please restart the program to re-try");
            }
            catch(Exception e)
            {
                Console.WriteLine("ERROR: an unforseen error occurred trying to set up the connection to the manager:\n" + e.Message);
            }
            
            
            Console.WriteLine("Press Enter to Exit");
            Console.ReadLine();
            
        }

        /// <summary>
        ///             Utility Method to ping for connectivity.
        /// </summary>
        /// <returns></returns>
        public string ping()
        {
            return "Pong from recovery service";
        }

        /// <summary>
        ///             Allows manager to set unique service id
        /// </summary>
        /// <param name="serviceID"></param>
        public void setServiceID(int serviceID)
        {
            this.serviceID = serviceID;
        }

        /// <summary>
        ///             Allows manager to get unique service id.
        /// </summary>
        /// <returns></returns>
        public int getServiceID()
        {
            return serviceID;
        }

        
        /// <summary>
        ///             Assigns this service a batch of passwords to test.
        ///             Job is processed asynchronously in asyncDoBatch()
        /// </summary>
        /// <param name="batch">    Struct containing batch info, including charSet, starting permutation number and ending permutation number. </param>
        public void assignBatch(Batch batch)
        {
            //batch = new Batch();
            this.poisonPill = false;
            this.jobID = batch.jobID;
            this.charSet = batch.charSet;
            this.pwLength = batch.pwLength;

            BatchDelegate workDelegate = this.asyncDoBatch;      
            AsyncCallback callbackDelegate = asyncBatchComplete;

            int id = serviceID;
            Boolean wantMore = true;
            Boolean pwFound = true;
            String password = "";

            workDelegate.BeginInvoke(ref batch, out wantMore, out pwFound, out password, callbackDelegate, null);
        }


        /// <summary>
        ///             Asynchronous function that checks each permutation in batch to see if it matches the encrypted password.
        ///             This function works by taking the a permutation number, converting it to n-ary form where n is the size of the charSet.
        ///             This n-ary form of the charset is used to convert to a password plaintext string for testing.
        ///             This function loops through each permutation, starting from startPernNum and ending in endPermNum and tries each.
        ///             The results of this function are extracted in asyncBatchComplete()
        /// </summary>
        /// <param name="batch">        Batch Struct containing start permutation, end permutation and other info</param>
        /// <param name="wantMore">     Flag for service to indicate whether it wants more or not (For future version where user can flag to exit)</param>
        /// <param name="pwFound">      Flag for whether the password was found or not. </param>
        /// <param name="password">     The password if found. </param>
        private void asyncDoBatch(ref Batch batch, out Boolean wantMore, out Boolean pwFound, out String password)
        {

            wantMore = true;            //Whether this service wants more batches. Kept for future versions.
            pwFound = false;            //password found or not.
            password = "";              //empty init for out param.


            long startPermNum = batch.startPermNumber;      //The permutation number to start cracking from. eg permutation number 62 would be "0000z" for 5 letter.
            long endPermNum = batch.endPermNumber;          //Permutation to stop on, inclusive.
            String encryptedPW = batch.encryptedPW;         //The encrypted form of password.

            
            Console.WriteLine("Allocated from: " + startPermNum + " to " + endPermNum + " with encryption string = " + encryptedPW);

            //Loop through all assigned permutations, and check.
            for (long permNumber = startPermNum; permNumber <= endPermNum; permNumber++)
            {
                String permString = convertToPermutation(permNumber);
                String permutationEncrypted = pwdEncrypter.EncryptString(permString);
                //Console.WriteLine("Perm Number: " + permNumber + ", permString: " + permString);

                if (permutationEncrypted.Equals(encryptedPW))
                {
                    Console.WriteLine("FOUND MATCH: " + encryptedPW + " = " + permString +". Ending batch early.");
                    pwFound = true;                     //update the out variables
                    password = permString;              //out variable.
                    permNumber = endPermNum + 1;        //Break out of loop. I didn't want another boolean value in the for loop as this would slow the cracking down. And speed is important in this context! We want to crack as fast as possible.
                }

                if(poisonPill)
                {
                    permNumber = endPermNum + 1;        //Break out of loop, because manager has flagged to quit.
                }

            }

            if (!poisonPill)
            {
                Console.WriteLine("Batch Finished! (Completed permuations " + startPermNum + " to " + endPermNum + ")");
            }
            else
            {
                Console.WriteLine("Batch finished prematurely due to poison pill");
            }
            Console.Out.Flush();
        }

        /// <summary>
        ///             The callback delegate of the async asyncDoBatch() function will point to this.
        ///             This function extracts the results of asyncDoBatch() and calls the manager to signal batch complete.
        /// </summary>
        /// <param name="asyncResult"></param>
        private void asyncBatchComplete(IAsyncResult asyncResult)
        {
            AsyncResult asyncObj = (AsyncResult)asyncResult;

            if (asyncObj.EndInvokeCalled == false)
            {
                BatchDelegate batchDel = (BatchDelegate)asyncObj.AsyncDelegate;

                Batch batch = new Batch();                      //out param for endInvoke
                Boolean wantMore = true;                        //out param for endInvoke
                Boolean pwFound = false;                        //out param for endInvoke   
                String password = "<Error: not the password>";  //out param for endInvoke

                batchDel.EndInvoke(ref batch, out wantMore, out pwFound, out password, asyncObj);

                try
                {
                    manager.batchComplete(batch, this.serviceID, this.wantMore, pwFound, password);
                }
                catch(CommunicationException ce)
                {
                    Console.WriteLine("ERROR: failed to connect to RecoveryManager Server:\n" + ce.Message);
                }
                catch(TimeoutException te)
                {
                    Console.WriteLine("ERROR: Service timed out trying to mark batch as complete. Trying one last time...:");
                    try
                    {
                        manager.batchComplete(batch, this.serviceID, this.wantMore, pwFound, password);
                    }
                    catch(CommunicationException ce2)
                    {
                        Console.WriteLine("ERROR: failed to connect to RecoveryManager Server after 1 timeout:\n" + ce2.Message);
                    }
                    catch(TimeoutException te2)
                    {
                        Console.WriteLine("ERROR: Service timed out for a second time trying to mark batch as complete. Please restart the service.");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: (2nd Timeout) Failed to deliver finished batch to manager due to unexpected error:\n" + e.Message);
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("ERROR: Failed to deliver finished batch to manager due to unexpected error:\n" + e.Message);
                }
            }
            asyncObj.AsyncWaitHandle.Close();
        }

        public void setPoisonPill()
        {
            poisonPill = true;
        }

        /// <summary>
        ///             Utility method that converts a permutation number to a plain text password string
        /// </summary>
        /// <param name="permNumber">       The permutation to convert. </param>
        /// <returns></returns>
        private String convertToPermutation(long permNumber)
        {
            int[] charIndexes = new int[pwLength];          //An array of indexes, where each index is the index of the letter. Eg, an index of 0 is '0', while 11 is 'A', and 12 is 'B' etc.

            int baseNumber = charSet.Length;                //The base number when converting from n-ary. Eg if charSet had only 2 characters, it would be a binary conversion and baseNum is 2.

            int ii = pwLength - 1;                          //Start from the last character in the permutation, and work backwards.
            long tempPermNumber = permNumber;
            while (tempPermNumber > 0)                      //Convert the permuation number into n-ary form. If it was just two chars in charset its convert to binary, for 3 its ternary, for n its n-ary
            {
                long value = tempPermNumber % baseNumber;   //A value between 0 and 61 for default charSet.
                charIndexes[ii] = (int)value;               //Put the index value in the array.
                tempPermNumber = tempPermNumber / baseNumber;   //Get the quotient. 
                ii--;
            }


            char[] permutation = new char[pwLength];        //The permuation (ie password) we are trying to generate.
            for (int index = 0; index < pwLength; index++)
            {
                int charIndex = charIndexes[index];         //Now its just a case of looking up the character associated with each index.
                char c = charSet[charIndex];
                permutation[index] = c;
            }

            String permString = new String(permutation);    //Convert to String.

            return permString;
        }
        
    }
}
