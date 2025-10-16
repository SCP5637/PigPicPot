using System;
using System.Windows.Input;

namespace PigPicPot.ViewModels
{
    /// <summary>
    /// 泛型RelayCommand，用于将命令绑定到执行方法
    /// Generic RelayCommand, used to bind commands to execution methods
    /// </summary>
    /// <typeparam name="T">命令参数类型</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;

        /// <summary>
        /// 构造函数，初始化命令
        /// Constructor, initialize command
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">是否可以执行的判断方法</param>
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 命令可执行状态更改事件
        /// Command can execute status changed event
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// Determine if command can be executed
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>是否可以执行</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || (parameter is T t && _canExecute(t));
        }

        /// <summary>
        /// 执行命令
        /// Execute command
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public void Execute(object? parameter)
        {
            if (_canExecute == null || parameter is T t)
            {
                _execute(parameter is T p ? p : default(T)!);
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
}