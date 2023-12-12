namespace Brimborium.OrleansMaerchen.Grains;

public interface ITodoGrain : IGrainWithGuidKey {
    ValueTask<OptionalResult<Todo>> GetAsync();
    ValueTask<OptionalResult<Todo>> SetAsync(Todo todo);
    ValueTask<OptionalResult<Todo>> SetPartialAsync(TodoPartial todoPartial);
}

public class TodoGrain
    : Grain
    , ITodoGrain {
    private readonly IPersistentState<Todo> _State;
    private readonly ILogger _Logger;

    public TodoGrain(
        [PersistentState("Todo", "Todo")] IPersistentState<Todo> state,
        ILogger<TodoGrain> logger
        ) {
        this._State = state;
        this._Logger = logger;
    }

    public async ValueTask<OptionalResult<Todo>> GetAsync() {
        if (this._State.RecordExists) {
            return (this._State.State).AsOptionalResult();
        } else {
            await Task.Yield();
            //this.DeactivateOnIdle();
            return NoValue.Value;
        }
    }

    public async ValueTask<OptionalResult<Todo>> SetAsync(Todo todo) {
        try {
            var pk = this.GetPrimaryKey();
            var recordExists = this._State.RecordExists;
            var stateValue = (recordExists) ? this._State.State : new Todo() with { TodoId = pk };

            var stateValueNext = stateValue with { 
                Name = todo.Name.Trim(),
                Done = todo.Done
            };

            if (this.Validate(stateValueNext).TryGetError(out var error)) { return error; }

            return await SaveTodo(stateValue, stateValueNext);
        } catch (Exception error) {
            return ErrorValue.CreateFromCatchedException(error);
        }
    }

    public async ValueTask<OptionalResult<Todo>> SetPartialAsync(TodoPartial todoPartial) {
        try {
            var pk = this.GetPrimaryKey();
            var recordExists = this._State.RecordExists;
            var stateValue = (recordExists) ? this._State.State : new Todo() with { TodoId = pk };
            var stateValueNext = todoPartial.Apply(stateValue);

            stateValueNext = stateValueNext with {
                Name = stateValueNext.Name.Trim()
            };

            if (string.IsNullOrWhiteSpace(stateValueNext.Name)) {
                return new ErrorValue(new ArgumentNullException(nameof(stateValueNext.Name)));
            }

            if (this.Validate(stateValueNext).TryGetError(out var error)) { return error; }

            return await SaveTodo(stateValue, stateValueNext);
        } catch (Exception error) {
            return ErrorValue.CreateFromCatchedException(error);
        }
    }

    private async ValueTask<Todo> SaveTodo(Todo stateValue, Todo stateValueNext) {
        if (this._State.RecordExists && stateValue.Equals(stateValueNext)) {
            return stateValue;
        } else {
            this._State.State = stateValueNext;
            await this._State.WriteStateAsync();
            return this._State.State;
        }
    }

    public OptionalErrorValue Validate(Todo todo) {
        if (string.IsNullOrWhiteSpace(todo.Name)) {
            return new ArgumentNullException(nameof(todo.Name));
        }
        return default;
    }
}
