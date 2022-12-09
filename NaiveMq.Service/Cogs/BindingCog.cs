﻿using NaiveMq.Service.Entities;
using System.Text.RegularExpressions;

namespace NaiveMq.Service.Cogs
{
    public class BindingCog
    {
        public BindingEntity Entity { get; set; }

        public Regex Pattern { get; set; }

        public BindingCog(BindingEntity entity)
        {
            Entity = entity;
        }
    }
}
