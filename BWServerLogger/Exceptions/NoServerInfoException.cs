﻿using System;

namespace BWServerLogger.Exceptions
{
    [Serializable]
    public class NoServerInfoException : Exception
    {
        public NoServerInfoException()
        {
        }

        public NoServerInfoException(string message) : base(message)
        {
        }

        public NoServerInfoException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
