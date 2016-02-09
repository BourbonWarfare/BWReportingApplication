﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BWServerLogger.Model
{
    public class Session
    {

        public int Id
        {
            get;
            set;
        }

        public string HostName
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }

        public int MaxPlayers
        {
            get;
            set;
        }

        public long MaxPing
        {
            get;
            set;
        }

        public long MinPing
        {
            get;
            set;
        }

        public DateTime Date
        {
            get;
            set;
        }

        public Session()
        {
        }

        public override int GetHashCode()
        {
            return Date.GetHashCode();
        }
    }
}