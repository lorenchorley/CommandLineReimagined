using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace InteractionLogic.FrameworkAccessors;


public abstract class FrameworkElementAccessor<TValue> where TValue : FrameworkElement
{
    private Dictionary<Type, Action<TValue>> _addEventHandlers = new();
    private Dictionary<Type, Action<TValue>> _removeEventHandlers = new();

    protected TValue? Value { get; private set; }

    public void Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(Value, nameof(Value));

        var dispatcher = Value.Dispatcher;

        dispatcher.BeginInvoke(action, DispatcherPriority.Render);
    }

    public void SetFrameworkElement(TValue value)
    {
        UnsetFrameworkElement();

        Value = value;

        _addEventHandlers.ForEach(p => p.Value(value));
    }

    public void UnsetFrameworkElement()
    {
        if (Value != null)
        {
            _removeEventHandlers.ForEach(p => p.Value(Value));

            Value = null;
        }
    }

    public void RegisterEventHandlers<TAssociatiedClass>(Action<TValue> addEventHandlers, Action<TValue> removeEventHandlers)
    {
        _addEventHandlers.Add(typeof(TAssociatiedClass), addEventHandlers);
        _removeEventHandlers.Add(typeof(TAssociatiedClass), removeEventHandlers);

        if (Value != null)
        {
            addEventHandlers(Value);
        }
    }

    public void UnregisterEventHandlers<TAssociatedClass>()
    {
        if (_removeEventHandlers.TryGetValue(typeof(TAssociatedClass), out var removeEventHandlers))
        {
            if (Value != null)
            {
                removeEventHandlers(Value);
            }

            _removeEventHandlers.Remove(typeof(TAssociatedClass));
        }
    }

}

public abstract class KeyedFrameworkElementAccessor<TValue> where TValue : FrameworkElement
{
    private Dictionary<string, TValue> Values { get; } = new();

    public bool TryGet(string key, [NotNullWhen(true)] out TValue? value) 
        => Values.TryGetValue(key, out value);

    public void AddFrameworkElement(string key, TValue value)
    {
        if (Values.ContainsKey(key))
        {
            Values[key] = value;
        }
        else
        {
            Values.Add(key, value);
        }
    }

}

public abstract class FrameworkElementAccessor<TValue1, TValue2> where TValue1 : FrameworkElement where TValue2 : FrameworkElement
{
    private Dictionary<Type, Action<TValue1, TValue2>> _addEventHandlers = new();
    private Dictionary<Type, Action<TValue1, TValue2>> _removeEventHandlers = new();

    protected TValue1? Value1 { get; private set; }
    protected TValue2? Value2 { get; private set; }

    public void Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(Value1, nameof(Value1));

        var dispatcher = Value1.Dispatcher;

        dispatcher.BeginInvoke(action, DispatcherPriority.Render);
    }

    public void SetFrameworkElement(TValue1 value1, TValue2 value2)
    {
        UnsetFrameworkElement();

        Value1 = value1;
        Value2 = value2;

        _addEventHandlers.ForEach(p => p.Value(value1, value2));
    }

    public void UnsetFrameworkElement()
    {
        if (Value1 != null && Value2 != null)
        {
            _removeEventHandlers.ForEach(p => p.Value(Value1, Value2));

            Value1 = null;
            Value2 = null;
        }
    }

    public void RegisterEventHandlers<TAssociatiedClass>(Action<TValue1, TValue2> addEventHandlers, Action<TValue1, TValue2> removeEventHandlers)
    {
        _addEventHandlers.Add(typeof(TAssociatiedClass), addEventHandlers);
        _removeEventHandlers.Add(typeof(TAssociatiedClass), removeEventHandlers);

        if (Value1 != null && Value2 != null)
        {
            addEventHandlers(Value1, Value2);
        }
    }

    public void UnregisterEventHandlers<TAssociatedClass>()
    {
        if (_removeEventHandlers.TryGetValue(typeof(TAssociatedClass), out var removeEventHandlers))
        {
            if (Value1 != null && Value2 != null)
            {
                removeEventHandlers(Value1, Value2);
            }

            _removeEventHandlers.Remove(typeof(TAssociatedClass));
        }
    }

}

