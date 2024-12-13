using System.Diagnostics;
using System.Globalization;
using TairitsuSora.Utils;
using YukariToolBox.LightLog;

namespace TairitsuSora.Commands.Othello;

public class FluorineSession : IDisposable
{
    public FluorineSession()
    {
        ProcessStartInfo procInfo = new()
        {
            FileName = "tools/fluorine",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        var proc = Process.Start(procInfo);
        _proc = proc ?? throw new InvalidOperationException("Failed to start Fluorine C++ backend");
    }

    public async ValueTask<Board> SetStateAsync(Board board)
    {
        char player = board.ActivePlayer == Board.CellType.Black ? 'b' : 'w';
        return ParseBoard(await RunCommand($"set {board.Black:016X}{board.White:016X}{player}"));
    }

    public async ValueTask<Board> GetStateAsync() => ParseBoard(await RunCommand("show"));

    public async ValueTask LoadModelAsync(string config) => await RunCommand($"load data/othello/{config}");

    public async ValueTask<Board> PlayAsync(Board.Coords? coords) =>
        ParseBoard(await RunCommand($"play {coords?.ToString() ?? "pass"}"));

    public async ValueTask<Board.Coords> SuggestMoveAsync() => Board.Coords.FromString(await RunCommand("suggest"));

    public async ValueTask<List<(Board.Coords coords, float score)>> AnalyzeAsync()
    {
        List<(Board.Coords coords, float score)> scores = [];
        string line = await RunCommand("analyze");
        string[] parts = line.SplitByWhitespaces();
        for (int i = 0; i < parts.Length; i += 2)
            scores.Add((Board.Coords.FromString(parts[i]),
                float.Parse(parts[i + 1], CultureInfo.InvariantCulture)));
        return scores;
    }

    public async void Dispose()
    {
        try { await Quit(); }
        catch { /* ignored */ }
    }

    private Process _proc;

    private static ArgumentException InvalidBoard() => new("Invalid board representation");

    private async ValueTask<string> RunCommand(string command)
    {
        if (_proc.HasExited)
        {
            Log.Warning("Fluorine", $"Fluorine exited unexpectedly with code {_proc.ExitCode}");
            string stdout = await _proc.StandardOutput.ReadToEndAsync();
            string stderr = await _proc.StandardError.ReadToEndAsync();
            Log.Warning("Fluorine", $"StdOut: {stdout}");
            Log.Warning("Fluorine", $"StdErr: {stderr}");
            throw new InvalidOperationException("Fluorine C++ backend exited unexpectedly");
        }
        await _proc.StandardInput.WriteLineAsync(command);
        await _proc.StandardInput.FlushAsync();
        string line = (await _proc.StandardOutput.ReadLineAsync())!;
        if (line == "error")
            throw new InvalidOperationException("Error in Fluorine C++ backend");
        return line;
    }

    private Board ParseBoard(string line)
    {
        if (line.Length != 50) throw InvalidBoard();
        if (!ulong.TryParse(line[..16], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var black))
            throw InvalidBoard();
        if (!ulong.TryParse(line[16..32], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var white))
            throw InvalidBoard();
        if (!ulong.TryParse(line[33..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var moves))
            throw InvalidBoard();
        Board.CellType activePlayer = line[32] switch
        {
            'b' => Board.CellType.Black,
            'w' => Board.CellType.White,
            _ => throw InvalidBoard()
        };
        bool ended = line[^1] == '+';
        return new Board(black, white, moves, activePlayer, ended);
    }

    private async ValueTask Quit()
    {
        await RunCommand("quit");
        await _proc.WaitForExitAsync();
        _proc.Dispose();
    }
}
