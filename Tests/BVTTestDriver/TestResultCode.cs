using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BVTTestDriver
{
    public enum TestResultCode
    {
        Success,
        QasTimeout,
        NoQasResult,
        QuerySkipped,
        WrongDomain,
        WrongIntent,
        DialogError
    }
}
