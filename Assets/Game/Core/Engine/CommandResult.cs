namespace Game.Core.Engine
{
    public readonly struct CommandResult
    {
        public bool Ok { get; }
        public string Error { get; }

        private CommandResult(bool ok, string error) { Ok = ok; Error = error; }

        public static readonly CommandResult Success = new(true, "");
        public static CommandResult Fail(string error) => new(false, error);

        public override string ToString() => Ok ? "OK" : $"FAIL: {Error}";
    }
}
