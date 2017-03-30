using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{
    /// <summary>
    ///             Interface that exposes functionality to the GUI client, and NOT the recovery services.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(IRecoveryManagerGUICallback))]
    public interface IRecoveryManagerGUI
    {
        /// <summary>
        ///             generates a new password of given length, and creates a new job based on this.
        /// </summary>
        /// <param name="length">   Length of the password to be generated, between 1 and 10 inclusive </param>
        /// <returns>   The encrypted password. If job already in progress, it returns existing jobs encrypted password </returns>
        [OperationContract]
        String generateNewPassword(int length);

        /// <summary>
        ///             Function to kickstart the program if connected via the web. 
        /// </summary>
        /// <returns>   The password </returns>
        /// <remarks>   The web function call needs to hold off returning until the password is found. So a semaphore is used to put a hold in it.
        ///             The semaphore is released when the password is found (ie method batchComplete(...) </remarks>
        [OperationContract]     
        String beginViaWeb();

        /// <summary>
        ///             Kickstarter function to begin the password recovery process.
        /// </summary>
        /// <remarks>   As workers beome free after initial assignment, they will be automatically assigned a new batch in their batchComplete(..) function. </remarks>
        [OperationContract(IsOneWay = true)]
        void begin();

        /// <summary>
        ///             Registers the GUI client.
        /// </summary>
        /// <returns>   True if successful, False if a GUI already connected. </returns>
        [OperationContract]
        Boolean addGUIClient();

        /// <summary>
        ///             De-registers the GUI client.
        /// </summary>
        [OperationContract]
        void GUIDisconnect();

        /// <summary>
        ///             Finds whether a job is in progress
        /// </summary>
        /// <returns>   False if job complete or not job ever created. </returns>
        [OperationContract]
        Boolean jobInProgress();

        /// <summary>
        ///             Bonus functionality: allows a user to enter a custom password to encrypt and try crack.
        /// </summary>
        /// <param name="customPW">     The plaintext password to encrypt and try crack. </param>
        /// <returns>   True if it was a valid password ie contains only valid characters from default char set 0-9, A-Z, a-z </returns>
        [OperationContract]
        Boolean setCustomPassword(String customPW, ref String encyptedPW);

        /// <summary>
        ///         Cancels the job at GUI / users request.
        /// </summary>
        [OperationContract]
        void cancelJob();


        [OperationContract]
        String ping();
        
        
        
        //This was a method when I had the statistics update as a service oriented approach. IE, if you want stats, come and get them. Why is it my job to update you? Maybe you don't want them anymore. 
        //But in keeping in line with the assignment specifications, I have put this method in the GUICallBack interface so that it is updated via a callback (and not service oriented).
        //[OperationContract]
        //void getJobStats(out int numConnected, out long numTested, out long rate, out long elapsedSeconds);


    }
}
