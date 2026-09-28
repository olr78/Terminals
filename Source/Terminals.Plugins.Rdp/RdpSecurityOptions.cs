using System;

namespace Terminals.Data
{
    [Serializable]
    public class RdpSecurityOptions
    {
        public Boolean Enabled { get; set; }
        public Boolean EnableEncryption { get; set; }
        
        public Boolean EnableTLSAuthentication { get; set; }
        public Boolean EnableNLAAuthentication { get; set; }

        /// <summary>
        /// Type the saved password on the server logon screen, when the server always asks for the password.
        /// </summary>
        public Boolean AutoTypePassword { get; set; }

        /// <summary>
        /// Seconds to wait after the connection is established, before the password is typed.
        /// </summary>
        public Int32 AutoTypePasswordDelay { get; set; }

        public RdpSecurityOptions()
        {
            this.AutoTypePasswordDelay = 3;
        }

        private string workingFolder;
        public String WorkingFolder
        {
            get
            {
                return this.workingFolder;
            }
            set
            {
                this.workingFolder = value;
            }
        }

        private string startProgram;
        public String StartProgram
        {
            get
            {
                return this.startProgram;
            }
            set
            {
                this.startProgram = value;
            }
        }

        internal RdpSecurityOptions Copy()
        {
            return new RdpSecurityOptions
                {
                    Enabled = this.Enabled,
                    EnableEncryption = this.EnableEncryption,
                    EnableNLAAuthentication = this.EnableNLAAuthentication,
                    EnableTLSAuthentication = this.EnableTLSAuthentication,
                    AutoTypePassword = this.AutoTypePassword,
                    AutoTypePasswordDelay = this.AutoTypePasswordDelay,
                    WorkingFolder = this.WorkingFolder,
                    StartProgram = this.StartProgram
                };
        }
    }
}
