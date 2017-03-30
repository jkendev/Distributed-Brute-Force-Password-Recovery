using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{
    [ServiceContract(CallbackContract = typeof(IRecoveryManagerServiceCallback))]
    public interface IRecoveryManagerService
    {
        //Synchronise both the registering and de-regisering in one method. Avoids racey conditions. 
        /// <summary>
        ///             Allows a recovery service to register or de-register at the manager.
        /// </summary>
        /// <param name="isRegistering">    Indicates whether registering or deregistering. </param>
        [OperationContract]
        void registerService(Boolean isRegistering);

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
        [OperationContract]
        void batchComplete(Batch batch, int serviceID, Boolean wantMore, Boolean pwFound, String password);


        [OperationContract]
        String ping();
    }
}
