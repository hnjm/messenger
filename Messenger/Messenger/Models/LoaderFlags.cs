namespace Messenger.Models
{
    public enum LoaderFlags
    {
        None,

        /// <summary>
        /// 在程序加载时执行
        /// </summary>
        OnLoad,

        /// <summary>
        /// 在程序退出时执行
        /// </summary>
        OnExit,
    }
}
