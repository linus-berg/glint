namespace Glint.Models;

public class UndoableAction
{
    public required Action Undo { get; init; }
    public required Action Redo { get; init; }
    public string Description { get; init; } = string.Empty;
}

public class UndoRedoManager
{
    private readonly Stack<UndoableAction> _undoStack = new();
    private readonly Stack<UndoableAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event Action? StateChanged;

    public void Execute(UndoableAction action)
    {
        action.Redo();
        _undoStack.Push(action);
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    public void AddWithoutExecuting(UndoableAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        StateChanged?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        StateChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }
}
