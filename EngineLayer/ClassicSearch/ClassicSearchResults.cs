﻿using System.Text;

namespace EngineLayer.ClassicSearch
{
    public class ClassicSearchResults : MyResults
    {

        #region Internal Constructors

        internal ClassicSearchResults(ClassicSearchEngine searchParams) : base(searchParams)
        {
        }

        #endregion Internal Constructors

        #region Public Properties

        public PsmClassic[][] OuterPsms { get; set; }

        #endregion Public Properties

        #region Protected Properties

        protected override string StringForOutput
        {
            get
            {
                var sb = new StringBuilder();
                return sb.ToString();
            }
        }

        #endregion Protected Properties

    }
}