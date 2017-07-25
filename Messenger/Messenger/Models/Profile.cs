﻿using Messenger.Foundation;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Messenger.Models
{
    [Serializable]
    [XmlRoot(ElementName = "profile")]
    public class Profile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        protected virtual void OnPropertyChanged(string name)
        {
            var arg = new PropertyChangedEventArgs(name);
            PropertyChanged?.Invoke(this, arg);
            StaticPropertyChanged?.Invoke(this, arg);
        }

        private int _id = 0;
        private int _hint = 0;
        private string _name = null;
        private string _text = null;
        private string _imag = null;

        [XmlElement(ElementName = "id")]
        public int ID
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(ID));
            }
        }

        [XmlIgnore]
        public int Hint
        {
            get => _hint;
            set
            {
                _hint = value;
                OnPropertyChanged(nameof(Hint));
            }
        }

        [XmlIgnore]
        public bool IsClient => _id > Server.ID;

        [XmlIgnore]
        public bool IsGroups => _id > Server.ID == false;

        [XmlElement(ElementName = "name")]
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [XmlElement(ElementName = "text")]
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        [XmlIgnore]
        public string Image
        {
            get => _imag;
            set
            {
                _imag = value;
                OnPropertyChanged(nameof(Image));
            }
        }

        public Profile CopyFrom(Profile profile, bool ignoreImage, bool ignoreid = true)
        {
            if (!ignoreid)
                ID = profile._id;
            if (!ignoreImage)
                Image = profile._imag;
            Name = profile._name;
            Text = profile._text;
            return this;
        }
    }
}