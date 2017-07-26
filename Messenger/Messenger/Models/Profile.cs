using Messenger.Foundation;
using System.ComponentModel;

namespace Messenger.Models
{
    public class Profile : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string name)
        {
            var arg = new PropertyChangedEventArgs(name);
            PropertyChanged?.Invoke(this, arg);
            StaticPropertyChanged?.Invoke(this, arg);
        }

        protected int _id = 0;
        protected int _hint = 0;
        protected string _name = null;
        protected string _text = null;
        protected string _imag = null;

        public int ID
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(ID));
            }
        }

        public int Hint
        {
            get => _hint;
            set
            {
                _hint = value;
                OnPropertyChanged(nameof(Hint));
            }
        }

        public bool IsClient => _id > Server.ID;

        public bool IsGroups => _id < Server.ID;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        public string Image
        {
            get => _imag;
            set
            {
                _imag = value;
                OnPropertyChanged(nameof(Image));
            }
        }

        public Profile CopyFrom(Profile profile, bool ignoreid = true)
        {
            if (!ignoreid)
                ID = profile._id;
            Image = profile._imag;
            Name = profile._name;
            Text = profile._text;
            return this;
        }
    }
}
