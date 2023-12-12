using Brimborium.ReturnValue;

namespace Brimborium.OrleansMaerchen.Contracts;

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public record class Todo(
    Guid TodoId,
    string Name,
    bool Done
) {
    public Todo() : this(Guid.Empty, string.Empty, false) {
    }
}

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public record class TodoPartial(
    Guid TodoId,
    OptionalValue<string> Name,
    OptionalValue<bool> Done
) {
    public TodoPartial() : this(Guid.Empty, new(), new()) {
    }

    public Todo Apply(Todo target) { 
        return target with {
            TodoId = this.TodoId,
            Name = this.Name.GetValueOrDefault(target.Name),
            Done = this.Done.GetValueOrDefault(target.Done)
        };
    }
}

