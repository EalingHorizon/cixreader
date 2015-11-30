﻿// *****************************************************
// CIXReader
// ConversationCollection.cs
// 
// Author: Steve Palmer (spalmer@cix)
// 
// Created: 29/08/2013 7:17 PM
// 
// Copyright (C) 2013-2015 CIX Online Ltd. All Rights Reserved.
// *****************************************************

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using CIXClient.Models;
using CIXClient.Tables;

namespace CIXClient.Collections
{
    /// <summary>
    /// The ConversationCollection class encapsulates functionality for working with a
    /// collection of conversations.
    /// </summary>
    public sealed class ConversationCollection : IEnumerable<InboxConversation>
    {
        private List<InboxConversation> _conversations;
        private DateTime _lastCheckDateTime = new DateTime(1900, 1, 1);

        /// <summary>
        /// Event handler for notifying a delegate that an inbox item has been updated.
        /// </summary>
        public event ConversationHandler ConversationAdded;

        /// <summary>
        /// Event handler for notifying a delegate that an inbox conversation has changed.
        /// </summary>
        public event ConversationHandler ConversationChanged;

        /// <summary>
        /// Event handler for notifying a delegate that a conversation has been deleted.
        /// </summary>
        public event ConversationHandler ConversationDeleted;

        /// <summary>
        /// Defines the delegate for InboxUpdated event notifications.
        /// </summary>
        /// <param name="sender">The InboxTasks object</param>
        /// <param name="e">Additional inbox update data</param>
        public delegate void ConversationHandler(object sender, InboxEventArgs e);

        /// <summary>
        /// Return the list of all active (non-deleted) conversations.
        /// </summary>
        public IEnumerable<InboxConversation> AllConversations
        {
            get
            {
                return Conversations.Where(msg => !msg.Flags.HasFlag(InboxConversationFlags.Deleted)).ToArray();
            }
        }

        /// <summary>
        /// Return the count of unread inbox messages
        /// </summary>
        public int TotalUnread
        {
            get
            {
                return AllConversations.Sum(conv => conv.UnreadCount);
            }
        }

        /// <summary>
        /// Return the count of unread priority inbox messages. Currently
        /// this is always 0.
        /// </summary>
        public int TotalUnreadPriority
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// Return the conversation with the specified remote ID.
        /// </summary>
        /// <param name="conversationID">Remote ID</param>
        /// <returns>Conversation with remote ID or null</returns>
        public InboxConversation ConversationByID(int conversationID)
        {
            return Conversations.FirstOrDefault(row => row.RemoteID == conversationID);
        }

        /// <summary>
        /// Add a new root message to the conversation collection.
        /// </summary>
        /// <param name="newRoot">The new conversation root</param>
        public void Add(InboxConversation newRoot)
        {
            lock (CIX.DBLock)
            {
                CIX.DB.Insert(newRoot);
            }
            _conversations.Add(newRoot);
        }

        /// <summary>
        /// Add a new conversation with a given message.
        /// </summary>
        /// <param name="conversation"></param>
        /// <param name="message"></param>
        public void Add(InboxConversation conversation, InboxMessage message)
        {
            if (conversation.RemoteID == 0)
            {
                Add(conversation);
            }
            message.ConversationID = conversation.ID;
            conversation.Messages.AddInternal(message);
            NotifyConversationAdded(conversation);
        }

        /// <summary>
        /// Returns an enumerator for iterating over the conversations.
        /// </summary>
        /// <returns>A generic enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator for iterating over the conversations.
        /// </summary>
        /// <returns>An enumerator for InboxConversation</returns>
        public IEnumerator<InboxConversation> GetEnumerator()
        {
            return (_conversations != null) ? _conversations.GetEnumerator() : new List<InboxConversation>.Enumerator();
        }

        /// <summary>
        /// Remove the specified conversation from the collection.
        /// </summary>
        /// <param name="conv">Conversation to remove</param>
        public void Remove(InboxConversation conv)
        {
            foreach (InboxMessage message in conv.Messages)
            {
                lock (CIX.DBLock)
                {
                    CIX.DB.Delete(message);
                }
            }
            _conversations.Remove(conv);
            lock (CIX.DBLock)
            {
                CIX.DB.Delete(conv);
            }
        }

        /// <summary>
        /// Retrieve new conversations from the server since the last time we checked.
        /// </summary>
        public void Refresh()
        {
            if (CIX.Online)
            {
                try
                {
                    List<InboxConversation> newFolders = new List<InboxConversation>();
                    List<CIXInboxItem> inboxItems = new List<CIXInboxItem>();

                    int totalCountOfRead = 0;

                    HttpWebRequest wrGeturl = APIRequest.GetWithQuery("personalmessage/inbox", APIRequest.APIFormat.XML, "since=" + _lastCheckDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    Stream objStream = APIRequest.ReadResponse(wrGeturl);

                    if (objStream != null)
                    {
                        using (XmlReader reader = XmlReader.Create(objStream))
                        {
                        XmlSerializer serializer = new XmlSerializer(typeof(ConversationInboxSet));
                        ConversationInboxSet inboxSet = (ConversationInboxSet)serializer.Deserialize(reader);

                            foreach (CIXInboxItem conv in inboxSet.Conversations)
                            {
                                inboxItems.Add(conv);
                                InboxConversation root = ConversationByID(conv.ID);
                                if (root == null)
                                {
                                    root = new InboxConversation
                                    {
                                        RemoteID = conv.ID,
                                        Date = DateTime.Parse(conv.Date),
                                        Subject = conv.Subject,
                                        Author = conv.Sender
                                    };
                                    Add(root);
                                    newFolders.Add(root);
                                }
                            }
                        }
                    }

                    wrGeturl = APIRequest.GetWithQuery("personalmessage/outbox", APIRequest.APIFormat.XML, "since=" + _lastCheckDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    objStream = APIRequest.ReadResponse(wrGeturl);

                    _lastCheckDateTime = DateTime.Now;

                    if (objStream != null)
                    {
                        using (XmlReader reader = XmlReader.Create(objStream))
                        {
                        XmlSerializer serializer = new XmlSerializer(typeof(ConversationOutboxSet));
                        ConversationOutboxSet inboxSet = (ConversationOutboxSet)serializer.Deserialize(reader);

                            foreach (CIXOutboxItem conv in inboxSet.Conversations)
                            {
                                // Make a fake CIXInboxItem for each CIXOutboxItem so we can treat
                                // them equally later.
                                inboxItems.Add(new CIXInboxItem
                                {
                                    Date = conv.Date,
                                    ID = conv.ID,
                                    Sender = conv.Recipient,
                                    Subject = conv.Body.FirstLine(),
                                    Unread = "false"
                                });
                                InboxConversation root = ConversationByID(conv.ID);
                                if (root == null)
                                {
                                    root = new InboxConversation
                                    {
                                        RemoteID = conv.ID,
                                        Date = DateTime.Parse(conv.Date),
                                        Subject = conv.Body.FirstLine(),
                                        Author = conv.Recipient
                                    };
                                    Add(root);
                                    newFolders.Add(root);
                                }
                            }
                        }
                    }

                    // Defer sending the notification of new root messages until we've got the whole list added
                    // to the database for performance reasons.
                    if (inboxItems.Count > 0)
                    {
                        NotifyConversationAdded(null);
                    }

                    // Once we've got the roots added, check each one for additions to the
                    // conversation.
                    foreach (CIXInboxItem conv in inboxItems)
                    {
                        InboxConversation root = ConversationByID(conv.ID);
                        if (root == null)
                        {
                            continue;
                        }

                        DateTime latestMessageDate = root.Date;

                        int countOfRead = GetConversation(root, ref latestMessageDate);
                        if (countOfRead > 0)
                        {
                            root.UnreadCount = (conv.Unread == "true") ? countOfRead : 0;
                            root.Flags &= ~InboxConversationFlags.MarkRead;
                            root.Date = latestMessageDate;
                            lock (CIX.DBLock)
                            {
                                CIX.DB.Update(root);
                            }
                        }

                        totalCountOfRead += countOfRead;
                    }
                    if (totalCountOfRead > 0)
                    {
                        NotifyConversationChanged(null);
                        LogFile.WriteLine("{0} new private messages retrieved from inbox", totalCountOfRead);
                    }
                }
                catch (Exception e)
                {
                    CIX.ReportServerExceptions("ConversationCollection.Refresh", e);
                }
            }
        }

        /// <summary>
        /// Contains the thread where inbox interactions with the server are contained. Events
        /// are fired from within the thread to the UI handler to indicate changes.
        /// </summary>
        internal void Sync()
        {
            try
            {
                PostMessages();
                Refresh();
            }
            catch (Exception e)
            {
                CIX.ReportServerExceptions("ConversationCollection.Sync", e);
            }
        }

        /// <summary>
        /// Send a notification that a conversation has been added
        /// </summary>
        internal void NotifyConversationAdded(InboxConversation conversation)
        {
            if (ConversationAdded != null)
            {
                ConversationAdded(this, new InboxEventArgs { Conversation = conversation });
            }
        }

        /// <summary>
        /// Send a notification that a conversation has been changed
        /// </summary>
        internal void NotifyConversationChanged(InboxConversation conversation)
        {
            if (ConversationChanged != null)
            {
                ConversationChanged(this, new InboxEventArgs { Conversation = conversation });
            }
        }

        /// <summary>
        /// Send a notification that a conversation has been deleted
        /// </summary>
        /// <param name="conversation"></param>
        internal void NotifyConversationDeleted(InboxConversation conversation)
        {
            if (ConversationDeleted != null)
            {
                ConversationDeleted(this, new InboxEventArgs { Conversation = conversation });
            }
        }

        /// <summary>
        /// Return the list of all conversations in the database.
        /// </summary>
        private IEnumerable<InboxConversation> Conversations
        {
            get { return _conversations ?? (_conversations = CIX.DB.Table<InboxConversation>().ToList()); }
        }

        /// <summary>
        /// Run the post message sync task. For every conversation that has
        /// a draft pending, we sync it.
        /// </summary>
        private void PostMessages()
        {
            if (CIX.Online)
            {
                // Make a copy because Sync may alter the Conversations collection.
                List<InboxConversation> localConv = new List<InboxConversation>(Conversations);
                foreach (InboxConversation conversation in localConv)
                {
                    conversation.Sync();
                }
            }
        }

        /// <summary>
        /// Retrieve an entire conversation. Any new messages are added to the message set and
        /// an event is fired.
        /// </summary>
        /// <param name="root">The root message to which this conversation belongs</param>
        /// <param name="latestMesssageDate">Ref to a DateTime that is set to the date of the
        /// most recent message in this conversation</param>
        private static int GetConversation(InboxConversation root, ref DateTime latestMesssageDate)
        {
            int countOfRead = 0;

            try
            {
                HttpWebRequest wrGeturl = APIRequest.Get("personalmessage/" + root.RemoteID + "/message", APIRequest.APIFormat.XML);
                Stream objStream = APIRequest.ReadResponse(wrGeturl);
                if (objStream != null)
                {
                    using (XmlReader reader = XmlReader.Create(objStream))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(PMessageSet));
                        PMessageSet inboxSet = (PMessageSet)serializer.Deserialize(reader);

                        foreach (CIXInboxMessage message in inboxSet.PMessages)
                        {
                            if (!root.Messages.Contains(message.MessageID))
                            {
                                InboxMessage newMessage = new InboxMessage
                                {
                                    ConversationID = root.ID,
                                    RemoteID = message.MessageID,
                                    Body = message.Body,
                                    Date = DateTime.Parse(message.Date),
                                    Author = message.Sender
                                };
                                root.Messages.AddInternal(newMessage);
                                if (newMessage.Date > latestMesssageDate)
                                {
                                    latestMesssageDate = newMessage.Date;
                                }
                                ++countOfRead;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CIX.ReportServerExceptions("ConversationCollection.GetConversation", e);
            }
            return countOfRead;
        }
    }
}