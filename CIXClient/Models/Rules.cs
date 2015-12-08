﻿// *****************************************************
// CIXReader
// Rules.cs
// 
// Author: Steve Palmer (spalmer@cix)
// 
// Created: 08/12/2015 09:07
//  
// Copyright (C) 2013-2015 CIX Online Ltd. All Rights Reserved.
// *****************************************************

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Xml.Schema;
using System.Xml.Serialization;
using CIXClient.Collections;
using CIXClient.Tables;

namespace CIXClient.Models
{
    /// <summary>
    /// Rules file
    /// </summary>
    [GeneratedCode("xsd", "4.0.30319.17929")]
    [Serializable]
    [DebuggerStepThrough]
    [DesignerCategory(@"code")]
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public sealed class rules
    {
        private RuleGroup[] itemsField;

        /// <remarks />
        [XmlElement("rulegroup", Form = XmlSchemaForm.Unqualified)]
        public RuleGroup[] Items
        {
            get { return itemsField; }
            set { itemsField = value; }
        }
    }

    /// <remarks />
    [GeneratedCode("xsd", "4.0.30319.17929")]
    [Serializable]
    [DebuggerStepThrough]
    [DesignerCategory(@"code")]
    [XmlType(AnonymousType = true)]
    public sealed class RuleGroup
    {
        private RuleActionCodes actionCodeField;
        private string groupField;
        private Rule[] ruleField;

        /// <remarks />
        [XmlElement("rule", Form = XmlSchemaForm.Unqualified)]
        public Rule[] rule
        {
            get { return ruleField; }
            set { ruleField = value; }
        }

        /// <remarks />
        [XmlAttribute]
        public string group
        {
            get { return groupField; }
            set { groupField = value; }
        }

        /// <remarks />
        [XmlElement(Form = XmlSchemaForm.Unqualified)]
        public RuleActionCodes actionCode
        {
            get { return actionCodeField; }
            set { actionCodeField = value; }
        }

        /// <summary>
        /// Computed criteria on rulegroup.
        /// </summary>
        [XmlIgnore]
        public Expression<Func<CIXMessage, bool>> Criteria { get; set; }
    }

    /// <remarks />
    [GeneratedCode("xsd", "4.0.30319.17929")]
    [Serializable]
    [DebuggerStepThrough]
    [DesignerCategory(@"code")]
    [XmlType(AnonymousType = true)]
    public sealed class Rule
    {
        private string operatorField;
        private string propertyField;
        private string valueField;

        /// <remarks />
        [XmlElement(Form = XmlSchemaForm.Unqualified)]
        public string value
        {
            get { return valueField; }
            set { valueField = value; }
        }

        /// <remarks />
        [XmlElement(Form = XmlSchemaForm.Unqualified)]
        public string op
        {
            get { return operatorField; }
            set { operatorField = value; }
        }

        /// <summary>
        /// Return the predicate operator for this rule
        /// </summary>
        public PredicateBuilder.Op? Operator
        {
            get
            {
                switch (operatorField)
                {
                    default:
                        return PredicateBuilder.Op.Equals;

                    case "equals":
                        return PredicateBuilder.Op.Equals;

                    case "contains":
                        return PredicateBuilder.Op.Contains;
                }
            }
        }

        /// <remarks />
        [XmlAttribute]
        public string property
        {
            get { return propertyField; }
            set { propertyField = value; }
        }
    }
}
