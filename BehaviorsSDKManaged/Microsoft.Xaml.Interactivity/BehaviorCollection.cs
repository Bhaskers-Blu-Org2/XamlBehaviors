﻿// -------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All Rights Reserved.
// -------------------------------------------------------------------
namespace Microsoft.Xaml.Interactivity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Windows.Foundation.Collections;
    using Windows.UI.Xaml;

    /// <summary>
    /// Represents a collection of IBehaviors with a shared <see cref="Microsoft.Xaml.Interactivity.BehaviorCollection.AssociatedObject"/>.
    /// </summary>
    public sealed class BehaviorCollection : DependencyObjectCollection
    {
        // After a VectorChanged event we need to compare the current state of the collection
        // with the old collection so that we can call Detach on all removed items.
        private readonly List<IBehavior> oldCollection = new List<IBehavior>();

        /// <summary>
        /// Initializes a new instance of the <see cref="BehaviorCollection"/> class.
        /// </summary>
        public BehaviorCollection()
        {
            this.VectorChanged += this.BehaviorCollection_VectorChanged;
        }

        /// <summary>
        /// Gets the <see cref="Windows.UI.Xaml.DependencyObject"/> to which the <see cref="BehaviorCollection"/> is attached.
        /// </summary>
        public DependencyObject AssociatedObject
        {
            get;
            private set;
        }

        /// <summary>
        /// Attaches the collection of behaviors to the specified <see cref="Windows.UI.Xaml.DependencyObject"/>.
        /// </summary>
        /// <param name="associatedObject">The <see cref="Windows.UI.Xaml.DependencyObject"/> to which to attach.</param>
        /// <exception cref="InvalidOperationException">The <see cref="BehaviorCollection"/> is already attached to a different <see cref="Windows.UI.Xaml.DependencyObject"/>.</exception>
        public void Attach(DependencyObject associatedObject)
        {
            if (associatedObject == this.AssociatedObject)
            {
                return;
            }

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            if (this.AssociatedObject != null)
            {
                throw new InvalidOperationException(ResourceHelper.GetString("CannotAttachBehaviorMultipleTimesExceptionMessage"));
            }

            Debug.Assert(associatedObject != null, "The previous checks should keep us from ever setting null here.");
            this.AssociatedObject = associatedObject;

            foreach (DependencyObject item in this)
            {
                IBehavior behavior = (IBehavior)item;
                behavior.Attach(this.AssociatedObject);
            }
        }

        /// <summary>
        /// Detaches the collection of behaviors from the <see cref="Microsoft.Xaml.Interactivity.BehaviorCollection.AssociatedObject"/>.
        /// </summary>
        public void Detach()
        {
            foreach (DependencyObject item in this)
            {
                IBehavior behaviorItem = (IBehavior)item;
                if (behaviorItem.AssociatedObject != null)
                {
                    behaviorItem.Detach();
                }
            }

            this.AssociatedObject = null;
            this.oldCollection.Clear();
        }

        private void BehaviorCollection_VectorChanged(IObservableVector<DependencyObject> sender, IVectorChangedEventArgs eventArgs)
        {
            if (eventArgs.CollectionChange == CollectionChange.Reset)
            {
                foreach (IBehavior behavior in this.oldCollection)
                {
                    if (behavior.AssociatedObject != null)
                    {
                        behavior.Detach();
                    }
                }

                this.oldCollection.Clear();

                foreach (DependencyObject newItem in this)
                {
                    this.oldCollection.Add(this.VerifiedAttach(newItem));
                }

#if DEBUG
                this.VerifyOldCollectionIntegrity();
#endif
                return;
            }

            int eventIndex = (int)eventArgs.Index;
            DependencyObject changedItem = this[eventIndex];

            switch (eventArgs.CollectionChange)
            {
                case CollectionChange.ItemInserted:
                    this.oldCollection.Insert(eventIndex, this.VerifiedAttach(changedItem));

                    break;

                case CollectionChange.ItemChanged:
                    IBehavior oldItem = this.oldCollection[eventIndex];
                    if (oldItem.AssociatedObject != null)
                    {
                        oldItem.Detach();
                    }

                    this.oldCollection[eventIndex] = this.VerifiedAttach(changedItem);

                    break;

                case CollectionChange.ItemRemoved:
                    oldItem = this.oldCollection[eventIndex];
                    if (oldItem.AssociatedObject != null)
                    {
                        oldItem.Detach();
                    }

                    this.oldCollection.RemoveAt(eventIndex);
                    break;

                default:
                    Debug.Assert(false, "Unsupported collection operation attempted.");
                    break;
            }

#if DEBUG
            this.VerifyOldCollectionIntegrity();
#endif
        }

        private IBehavior VerifiedAttach(DependencyObject item)
        {
            IBehavior behavior = item as IBehavior;
            if (behavior == null)
            {
                throw new InvalidOperationException(ResourceHelper.GetString("NonBehaviorAddedToBehaviorCollectionExceptionMessage"));
            }

            if (this.oldCollection.Contains(behavior))
            {
                throw new InvalidOperationException(ResourceHelper.GetString("DuplicateBehaviorInCollectionExceptionMessage"));
            }

            if (this.AssociatedObject != null)
            {
                behavior.Attach(this.AssociatedObject);
            }

            return behavior;
        }

        [Conditional("DEBUG")]
        private void VerifyOldCollectionIntegrity()
        {
            bool isValid = (this.Count == this.oldCollection.Count);
            if (isValid)
            {
                for (int i = 0; i < this.Count; i++)
                {
                    if (this[i] != this.oldCollection[i])
                    {
                        isValid = false;
                        break;
                    }
                }
            }

            Debug.Assert(isValid, "Referential integrity of the collection has been compromised.");
        }
    }
}
