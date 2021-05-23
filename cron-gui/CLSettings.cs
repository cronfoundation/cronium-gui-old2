using CommandLine;
using CommandLine.Text;

namespace Neo
{ 
    public class CLSettings
    {
        [Option]
        public string Wallet { set; get; }
        [Option]
        public string Password { set; get; }
    }
}