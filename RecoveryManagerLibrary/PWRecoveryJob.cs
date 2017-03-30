using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{

    /// <summary>
    ///     Class to encapsulate all housekeeping data associated with a job.
    ///     Main purpose is to keep track of which batches have been generated, and to generate new ones.
    /// </summary>
    internal class PWRecoveryJob
    {
        private String jobID;                   //ID of job, book keeping to avoid confusion among concurrent jobs
        private String encryptedPW;             //The password in encrypted form
        private char[] charSet;                 //The set of chars the password can be made from
        private int pwLength;                   //The length of the password

        private Boolean inProgress;             //Whether the job is in progress or not
        private Boolean allAssigned;            //Whether all permutations of the pw space are assigned.
        private Boolean pwFound = false;        //Whether the password has been found or not.
        private String recoveredPassword;       //The password if found.

        private long numPossiblePermutations;   //Total number of passwords possible for given length.
        private long batchSize;                 //number of permutations in a batch (Job split into batch)
        private int numBatches;                 //Number of batches. 
        private long permutationsComplete;      //how many passwords done so far
        private Stopwatch stopwatch;            //To get lapsed milliseconds.
        private Boolean timingBegun;            //To check if currently timed.

        private long estimatedRate = 150000;      //Estimated rate a service works at. I've measured it at about 9000 permutations / sec
        private long targetBatchTimeSec = 10;           //Target time you want your service to spend on each batch.

        private long batchMaxTimeSec = 10;      //Max time (in seconds) a batch can take. Nominal time of 10 seconds.

      
        private long nextBatchToAssign;          //The index of the next batch to assign in a sequence of batches, eg if batchSize = 10, this will be 0 for (0-9), 1 for (10-19), 2 for (20-29) etc
        //private double lastBatchToAssign;


        /// <summary>
        ///             Constructor
        /// </summary>
        /// <param name="jobID">            ID of job (Not really used, more showcasing extendability if had concurrent jobs) </param>
        /// <param name="encryptedPW">      Password in encrypted form </param>
        /// <param name="charSet">          charSet the password can be made from. </param>
        /// <param name="pwLength">         Length of plaintext password. </param>
        public PWRecoveryJob(String jobID, String encryptedPW, char[] charSet, int pwLength)
        {
            //Input validation:
            if (jobID == null || jobID.Length == 0)
            {
                throw new ArgumentException("PWRecoveryJob constrctor error: Invalid jobID");   //TODO: will gui see this? If so better less coupled message.
            }
            else if (encryptedPW == null || encryptedPW.Length == 0)
            {
                throw new ArgumentException("PWRecoveryJob constrctor error: Invalid encryptedPW");
            }
            else if (pwLength <= 0)
            {
                throw new ArgumentException("PWRecoveryJobConstructor error: pwLength is 0 or less (must be positive)");
            }

            //Now check if there is a duplicate letter in the charSet
            Boolean duplicateFound = false;
            for (int ii = 0; ii < charSet.Length; ii++)
            {
                for (int jj = 0; jj < charSet.Length; jj++)
                {
                    if (ii != jj && charSet[ii] == charSet[jj])
                    {
                        duplicateFound = true;
                    }
                }
            }
            if (duplicateFound)
            {
                throw new ArgumentException("PWRecoveryJob constrctor error: charSet contains duplicate characters");   //TODO must catch this. Include in sig
            }

            this.jobID = jobID;
            this.encryptedPW = encryptedPW;
            this.charSet = charSet;
            this.pwLength = pwLength;
            this.inProgress = false;
            this.allAssigned = false;
            this.recoveredPassword = "";
            //Set the number of possible permutations. IE, the total number of posssible passwords for the given length;
            long total = 1;
            for(int ii = 0; ii < pwLength; ii ++)
            {
                total = total * charSet.Length;
            }

            this.numPossiblePermutations = total;
            
            this.permutationsComplete = 0;
            this.stopwatch = new Stopwatch();
            this.timingBegun = false;
            this.nextBatchToAssign = 0;
        }

        public void batchCompleted(long startPermNum, long endPermNum)
        {
            permutationsComplete = permutationsComplete + (endPermNum - startPermNum);
            
        }

        /// <summary>
        ///             Gets the next batch in the sequence, starting from 0.
        /// </summary>
        /// <returns>   Container class that holds the batch info. </returns>
        public PWBatch getNextBatch()
        {
            //if(nextBatchToAssign < 0 || nextBatchToAssign > numPossiblePermutations -1 )
            
            PWBatch batch = null;
            if (!allAssigned)
            {
                inProgress = true;
                long start = nextBatchToAssign * batchSize;
                long end = start + batchSize - 1;            //Because start is included, take one off the end to get actual batchSize sized batch :P
                nextBatchToAssign++;
                if (end > numPossiblePermutations - 1)      //0 based indexing of permutations.
                {
                    end = numPossiblePermutations - 1;      //limit to end if overflows. Eg, start = 95, batchsize = 10, total number = 100, last batch will be size 5 not 10.
                    allAssigned = true;                     //No more batches to be had.
                }

                Console.WriteLine("GetNext: from " + start + " to " + end);
                // public PWBatch(int startPermNumber, int endPermNumber, String jobID, String encryptedPW, char[] charSet, int pwLength)
                batch = new PWBatch(start, end, this.jobID, this.encryptedPW, this.charSet, this.pwLength);
                
                if (!timingBegun)
                {
                    stopwatch.Start();
                    
                }
            }
            return batch;                                   //Could be null. TODO: or throw exception?
        }

        


        /************************************************ SETTERS **********************************************************/
        
        /// <summary>
        ///             A way to calibrate how long you want your batch to take. Setting this will modify the size of the 
        ///             batches. Therefore one should be careful to set the estimated password test rate of the services.
        /// </summary>
        /// <param name="timeInSec">    Target time in seconds for a batch to complete </param>
        public void setTargetBatchTime(long timeInSec)
        {
            this.targetBatchTimeSec = timeInSec;
        }

        /// <summary>
        ///             A way to calibrate the batch size. 
        /// </summary>
        /// <param name="estimatedRate">        An estimated rate in passwords per second. Initial tests show around 9000 per second on a single machine</param>
        public void setEstimatedServiceRate(long estimatedRate)
        {
            this.estimatedRate = estimatedRate;
        }

        /// <summary>
        ///             Sets the batch size based on number of services. 
        /// </summary>
        /// <param name="numServices">      Number of connected services. </param>
        /// <returns></returns>
        /// <remarks>   The batch size is capped at targetBatchTimeSec * estimatedRate 
        ///             If its smaller than this, then it does an even split.
        /// </remarks>
        public long setBatchSize(int numServices) 
        {
            long targetSize = targetBatchTimeSec * estimatedRate;      // permutations = Seconds * permutations/seconds
            long evenSplit = 0;
            if (!inProgress)
            {
                if (numServices <= 0)
                {
                    numServices = 1;    //Assume 1 if none connected.
                }
                //Split it evenly among all current services first. (If too big, change it next)
                evenSplit = (numPossiblePermutations / (long)numServices);
               
                //Put an upper limit on batch size so that workers are not spending hours on a batch. (If they d/c, you lose hours of work at once!)
                if (evenSplit > targetSize)
                {
                    
                    this.batchSize = targetSize;
                    Console.WriteLine("An even split of permutations gives " + evenSplit + " per batch, so it has been capped to : " + this.batchSize);
                }
                else
                {
                    this.batchSize = evenSplit;
                    Console.WriteLine("Batchsize set to : " + this.batchSize + "(Even split among services)");
                }
                
            }
            else
            {
                throw new InvalidOperationException("Cannot change batch size on job already in progress");  
            }
            return this.batchSize;
        }




        /// <summary>
        ///             Ends the job.
        /// </summary>
        public void endJob()
        {
            allAssigned = true;
            inProgress = false;
            stopwatch.Stop();
        }

        /// <summary>
        ///             Setter for the cracked plaintext password.
        /// </summary>
        /// <param name="password"></param>
        public void setRecoveredPassword(String password)
        {
            this.recoveredPassword = password;
            this.pwFound = true;
        }


        /************************************************ GETTERS **********************************************************/

        /// <summary>
        ///             Returns whether password has been found or not.
        /// </summary>
        /// <returns>   True if found, false otherwise. </returns>
        public Boolean passwordFound()
        {
            return pwFound;
        }

        /// <summary>
        ///             Gets recovered password.
        /// </summary>
        /// <returns></returns>
        public String getRecoveredPassword()
        {
            return recoveredPassword;
        }

        
        /// <summary>
        ///             Gets calculated rate in passwords per second
        /// </summary>
        /// <returns></returns>
        public long getRate()
        {
            long rate = 0;
            long lapsedSeconds = stopwatch.ElapsedMilliseconds / (long)1000;


            if (lapsedSeconds > 0)
            {
                rate = permutationsComplete / lapsedSeconds;        //No divide by 0 exception here!.
            }
            return rate;
            //return permutationsComplete / (stopwatch.ElapsedMilliseconds / 1000);
        }

        /// <summary>
        ///             Gets elapsed seconds since job begun.
        /// </summary>
        /// <returns></returns>
        public long getElapsedSconds()
        {
            return stopwatch.ElapsedMilliseconds / (long)1000;

        }

        /// <summary>
        ///             Gets total passwords tested so far.
        /// </summary>
        /// <returns></returns>
        public long getNumTested()
        {
            return permutationsComplete;
        }

        /// <summary>
        ///             Gets the job id
        /// </summary>
        /// <returns></returns>
        public String getJobID()
        {
            return jobID;
        }

        /// <summary>
        ///             Gets the encrypted password
        /// </summary>
        /// <returns></returns>
        public String getEncryptedPW()
        {
            return encryptedPW;
        }

        /// <summary>
        ///             gets the set of possible chars the pw is made from.
        /// </summary>
        /// <returns></returns>
        public char[] getCharSet()
        {
            return charSet;
        }

        /// <summary>
        ///             Gets the password length
        /// </summary>
        /// <returns></returns>
        public int getPWLength()
        {
            return pwLength;
        }

        /// <summary>
        ///             Returns whether this job is in progress or not.
        /// </summary>
        /// <returns></returns>
        public Boolean jobInProgress()
        {
            return inProgress;
        }

        /// <summary>
        ///             Returns whether this job is completed or not.
        /// </summary>
        /// <returns></returns>
        public Boolean jobCompleted()
        {
            return !inProgress && allAssigned;
        }

        /// <summary>
        ///             Returns true if some batches are available.
        ///             Note: batches might not be available, but being worked on.
        /// </summary>
        /// <returns></returns>
        public Boolean availableBatches()
        {
            return !allAssigned;    //this is dumb. I'd rather have a boolean called "availableBatches" but this creates ambiguity for the compiler between the variable and the method of the same name.
        }

        /// <summary>
        ///             Prints the job info.
        /// </summary>
        public void printJob()
        {
            Console.WriteLine("--- JOB INFO ---");
            Console.WriteLine(" pwLength = " + pwLength);
            Console.WriteLine(" numPossiblePermutations = " + numPossiblePermutations);
            Console.WriteLine(" Batch Size = " + batchSize);
            Console.WriteLine(" Num batches = " + numBatches);
            Console.WriteLine("--- -------- ---");
        }
    }
}
