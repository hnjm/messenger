using Mikodev.Network;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Messenger.Models
{
    public class Profile : INotifyPropertyChanging, INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler InstancePropertyChanged;

        public static event PropertyChangingEventHandler InstancePropertyChanging;

        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangingEventHandler PropertyChanging;

        private void _EmitChange<T>(ref T source, T target, [CallerMemberName] string name = null)
        {
            var eva = new PropertyChangingEventArgs(name);
            PropertyChanging?.Invoke(this, eva);
            InstancePropertyChanging?.Invoke(this, eva);

            if (Equals(source, target))
                return;
            source = target;

            var evb = new PropertyChangedEventArgs(name);
            PropertyChanged?.Invoke(this, evb);
            InstancePropertyChanged?.Invoke(this, evb);
        }

        private int _id = 0;
        private int _hint = 0;
        private string _name = null;
        private string _text = null;
        private string _imag = null;

        public bool IsClient => _id > Links.ID;

        public bool IsGroups => _id < Links.ID;

        public int ID
        {
            get => _id;
            set => _EmitChange(ref _id, value);
        }

        public int Hint
        {
            get => _hint;
            set => _EmitChange(ref _hint, value);
        }

        public string Name
        {
            get => _name;
            set => _EmitChange(ref _name, value);
        }

        public string Text
        {
            get => _text;
            set => _EmitChange(ref _text, value);
        }

        public string Image
        {
            get => _imag;
            set => _EmitChange(ref _imag, value);
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
