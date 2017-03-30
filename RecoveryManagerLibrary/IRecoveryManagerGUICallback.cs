using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{
    /// <summary>
    /// Callback interface for the GUI.
    /// </summary>
    [ServiceContract]
    public interface IRecoveryManagerGUICallback 
    {
        /// <summary>
        ///             Used to tell the GUI how many services connected to the Manager.
        /// </summary>
        /// <param name="numConnected">     Number of connected services. </param>
        [OperationContract(IsOneWay = true)]
        void setNumConnected(int numConnected);

        /// <summary>
        ///             Used to alert GUI that password was found.
        /// </summary>
        /// <param name="password">     The plain text password.</param>
        [OperationContract]
        void passwordFound(String password);

        /// <summary>
        ///             Callback function to update GUI about stats of the job. Occurs once every second in v1.0
        /// </summary>
        /// <param name="numConnected">     Number of connected services </param>
        /// <param name="numTested">        Number of tested passwords </param>
        /// <param name="rate">             APPROX Rate in passwords/second that has been tested.</param>
        /// <param name="elapsedSeconds">   Time since job started. </param>
        [OperationContract(IsOneWay = true)]
        void updateJobStats(int numConnected, long numTested, long rate, long elapsedSeconds);

    }
}

