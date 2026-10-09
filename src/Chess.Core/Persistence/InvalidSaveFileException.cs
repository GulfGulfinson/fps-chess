namespace Chess.Core.Persistence;

public sealed class InvalidSaveFileException(string message, Exception? inner = null) : Exception(message, inner);
