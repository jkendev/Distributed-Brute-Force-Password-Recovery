using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{
    /// <summary>
    /// A Struct to be passed across a network to a service. Smaller size than the PWBatch object!
    /// </summary>
    public struct Batch
    {
        public long startPermNumber;        //Starting permutation number
        public long endPermNumber;          //Ending permutation number
        public String jobID;                
        public String encryptedPW;
        public char[] charSet;              //Char set the password was constructed from.
        public int pwLength;                //Length of plain texst password.

        /// <summary>
        /// Constructor for the above Struct.
        /// </summary>
        public Batch(long startPermNumber, long endPermNumber, String jobID, String encryptedPW, char[] charSet, int pwLength)
            : this()
        {
            this.startPermNumber = startPermNumber;
            this.endPermNumber = endPermNumber;
            this.jobID = jobID;
            this.encryptedPW = encryptedPW;
            this.charSet = new char[charSet.Length];
            Array.Copy(charSet, this.charSet, charSet.Length);  //Source, Destination, legnth.
            this.pwLength = pwLength;
        }
    }


    /// <summary>
    /// NAME:           PWBatch
    /// PURPOSE:        To wrap the Batch struct and hold other housekeeping data
    /// REMARKS:        The services don't need to know about the housekeeping data. Thats why they are given a struct, not an object.
    ///                 Also, the PWBatch object allows a convenient way to implement the IEquitable interface, making it usable
    ///                 in several container classes (List and Queue). 
    /// </summary>
    internal class PWBatch : System.IEquatable<PWBatch>
    {

        private Batch batch;                //Struct of batch info
        private DateTime timeAssigned;      //Time the batch was assigned. (well, created, but they are assigned very quickly after being created)
        private IRecoveryManagerServiceCallback assignedService;    //The service this batch was assigned to.
        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="startPermNumber">      starting permutation number of the batch. </param>
        /// <param name="endPermNumber">        ending permutation of the batch - inclusive. </param>
        /// <param name="jobID">                ID of job the batch belongs to. </param>
        /// <param name="encryptedPW">          Encrypted form of password. </param>
        /// <param name="charSet">              char set the pw was formed from. </param>
        /// <param name="pwLength">             Length of plain text password. </param>
        public PWBatch(long startPermNumber, long endPermNumber, String jobID, String encryptedPW, char[] charSet, int pwLength)
        {
            batch = new Batch(startPermNumber, endPermNumber, jobID, encryptedPW, charSet, pwLength);
            timeAssigned = DateTime.Now;    
        }

        public void setAssignedService(IRecoveryManagerServiceCallback service)
        {
            this.assignedService = service;
        }

        public IRecoveryManagerServiceCallback getAssignedService()
        {
            return this.assignedService;
        }

        /// <summary>
        ///     An almost copy constructor. Takes the struct part of the batch and creates a new one.
        /// </summary>
        /// <param name="inBatch"></param>
        public PWBatch(Batch inBatch)
        {
            this.timeAssigned = DateTime.Now;
            this.batch = inBatch;
        }

        /// <summary>
        ///     Restarts the age of the batch (by setting time assigned to now).
        /// </summary>
        public void restartAge()
        {
            this.timeAssigned = DateTime.Now;
        }

        /// <summary>
        ///     Gets the age in milliseconds.
        /// </summary>
        /// <returns></returns>
        public long ageInMilliseconds()
        {
            DateTime current = DateTime.Now;
            TimeSpan ts = current - this.timeAssigned;

            return (long)ts.TotalMilliseconds;
        }

        /// <summary>
        ///             Gets the starting permutation number
        /// </summary>
        /// <returns></returns>
        public double getStartPermNumber()
        {
            return batch.startPermNumber;
        }

        /// <summary>
        ///             Gets the ending permutation number.
        /// </summary>
        /// <returns></returns>
        public double getEndPermNumber()
        {
            return batch.endPermNumber;
        }

        /// <summary>
        ///         Gets the inner Struct that wraps the batch data.
        /// </summary>
        /// <returns></returns>
        public Batch getBatch()
        {
            return batch;
        }
        
        //Needed for aList.Contains(someBatch) method in a list of password batches.
        /// <summary>
        ///         Implementation of Equals method from IEquitable interface.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        /// <remarks>   Needed so that this class can be used in List and Queue.
        ///             Matches on jobID, startPermNum and endPermNum
        ///             All else is irrelevant for the container class usage that I've implemented
        /// </remarks>
        public Boolean Equals(PWBatch other)
        {
            Batch otherBatch = other.getBatch();
            return batch.jobID.Equals(otherBatch.jobID) && batch.startPermNumber == otherBatch.startPermNumber && batch.endPermNumber == otherBatch.endPermNumber;

        }
    }
}
