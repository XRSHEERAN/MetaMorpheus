﻿namespace MetaMorpheusGUI
{
    public class RawData
    {
        #region Public Constructors

        public RawData(string FileName)
        {
            this.FileName = FileName;
            if (FileName != null)
                Use = true;
        }

        #endregion Public Constructors

        #region Public Properties

        public bool Use { get; set; }
        public string FileName { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public void AddFilePath(string FileName)
        {
            this.FileName = FileName;
            Use = true;
        }

        #endregion Public Methods
    }
}