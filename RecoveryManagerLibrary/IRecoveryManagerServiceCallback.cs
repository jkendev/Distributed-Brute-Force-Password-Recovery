using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{
    /// <summary>
    ///             Callback interface for the recovery services.
    ///             Distinct from the GUI callback interface (IRecoveryManagerGUICallback).
    ///             That way, the services don't need to have GUI methods implemented, and is more modular, more extendable, more maintainable.
    /// </summary>
    [ServiceContract]
    public interface IRecoveryManagerServiceCallback
    {
        /// <summary>
        ///             Assigns this service a batch of passwords to test.
        ///             Job is processed asynchronously in asyncDoBatch()
        /// </summary>
        /// <param name="batch">    Struct containing batch info, including charSet, starting permutation number and ending permutation number. </param>
        [OperationContract]
        void assignBatch(Batch batch);

        /// <summary>
        ///             Allows manager to set unique service id
        /// </summary>
        /// <param name="serviceID"></param>
        [OperationContract]
        void setServiceID(int serviceID);

        /// <summary>
        ///             Allows manager to get unique service id.
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        int getServiceID();


        [OperationContract]
        void setPoisonPill();
    

    

    }
}
