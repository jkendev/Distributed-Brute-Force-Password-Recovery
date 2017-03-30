using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RecoveryManagerLibrary
{
    public interface IRecoveryManager : IRecoveryManagerService, IRecoveryManagerGUI
    {
        //Need a method here or gets an error. So just put ping in again..
        [OperationContract]
        String ping();
    }
}
