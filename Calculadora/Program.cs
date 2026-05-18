using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

if (args.Any(a => string.Equals(a, "--console", StringComparison.OrdinalIgnoreCase)))
{
    ConsoleHost.EnsureConsole();
    RunConsole();
    return;
}

try
{
    await RunWebAsync(args);
}
catch (Exception ex)
{
    string message = $"Falha ao iniciar a Calculadora Web.{Environment.NewLine}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}";
    try
    {
        string logPath = StartupLog.Write(ex);
        message += $"{Environment.NewLine}{Environment.NewLine}Log: {logPath}";
    }
    catch
    {
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        MessageBox.Show("Calculadora", message);
    else
        Console.Error.WriteLine(message);
}

static void RunConsole()
{
    while (true)
    {
        if (!Console.IsOutputRedirected)
            Console.Clear();
        Console.WriteLine("=========================================");
        Console.WriteLine("       CALCULADORA CONSOLE - UNILAGO     ");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        double n1 = LerNumero("Digite o primeiro numero: ");
        double n2 = LerNumero("Digite o segundo numero:  ");

        Console.WriteLine();
        Console.WriteLine("Escolha a operacao:");
        Console.WriteLine("  1 - Soma (+)");
        Console.WriteLine("  2 - Subtracao (-)");
        Console.WriteLine("  3 - Multiplicacao (*)");
        Console.WriteLine("  4 - Divisao (/)");
        Console.WriteLine("  5 - Resto da divisao (%)");
        Console.WriteLine("  6 - Potencia (^)");
        Console.Write("Opcao: ");

        string? opcao = Console.ReadLine();
        Console.WriteLine();

        try
        {
            double resultado = Calcular(opcao, n1, n2);
            Console.WriteLine($"Resultado: {resultado:N4}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
        }

        Console.WriteLine();
        Console.Write("Deseja realizar outra operacao? (S/N): ");
        string? continuar = Console.ReadLine();
        if (!string.Equals(continuar?.Trim(), "S", StringComparison.OrdinalIgnoreCase))
            break;
    }

    Console.WriteLine("Encerrando a calculadora. Ate logo!");
}

static double LerNumero(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        string? entrada = Console.ReadLine();
        if (double.TryParse(entrada, NumberStyles.Any, CultureInfo.CurrentCulture, out double valor))
            return valor;

        Console.WriteLine("Entrada invalida. Tente novamente.");
    }
}

static double Calcular(string? opcao, double n1, double n2)
{
    return opcao switch
    {
        "1" or "+" => n1 + n2,
        "2" or "-" => n1 - n2,
        "3" or "*" => n1 * n2,
        "4" or "/" => n2 == 0 ? throw new DivideByZeroException("Divisao por zero nao e permitida.") : n1 / n2,
        "5" or "%" => n2 == 0 ? throw new DivideByZeroException("Divisao por zero nao e permitida.") : n1 % n2,
        "6" or "^" => Math.Pow(n1, n2),
        _ => throw new InvalidOperationException("Opcao invalida.")
    };
}

static async Task RunWebAsync(string[] args)
{
    int desiredPort = ParsePortArg(args) ?? 5005;
    int port = FindAvailablePort(desiredPort, 20);
    string baseUrl = $"http://localhost:{port}";

    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls(baseUrl);

    var app = builder.Build();

    var db = new CalcDb(GetDbPath(args));
    await db.InitAsync();

    app.MapGet("/", () =>
    {
        string? html = TryLoadHtml();
        return html is null
            ? Results.Text("Arquivo calculadora.html não encontrado.", "text/plain; charset=utf-8")
            : Results.Text(html, "text/html; charset=utf-8");
    });

    app.MapPost("/api/calc", async (HttpContext ctx) =>
    {
        CalcRequest? req;
        try
        {
            req = await ctx.Request.ReadFromJsonAsync<CalcRequest>();
        }
        catch
        {
            req = null;
        }

        if (req is null || string.IsNullOrWhiteSpace(req.Op))
            return Results.Json(new { ok = false, error = "Requisição inválida." }, statusCode: 400);

        try
        {
            double result = Calcular(req.Op, req.N1, req.N2);
            await db.LogAsync(new CalcLog(DateTime.UtcNow, req.N1, req.N2, req.Op, result, true, null));
            return Results.Json(new { ok = true, result });
        }
        catch (Exception ex)
        {
            await db.LogAsync(new CalcLog(DateTime.UtcNow, req.N1, req.N2, req.Op, null, false, ex.Message));
            return Results.Json(new { ok = false, error = ex.Message }, statusCode: 400);
        }
    });

    app.MapGet("/api/history", async (int? limit) =>
    {
        int take = limit is > 0 and <= 100 ? limit.Value : 20;
        List<CalcHistoryItem> items = await db.GetHistoryAsync(take);
        return Results.Json(new { ok = true, items });
    });

    await app.StartAsync();

    if (!args.Any(a => string.Equals(a, "--no-browser", StringComparison.OrdinalIgnoreCase)))
        TryOpenBrowser($"{baseUrl}/");

    await app.WaitForShutdownAsync();
}

static int? ParsePortArg(string[] args)
{
    foreach (string a in args)
    {
        if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
        {
            string raw = a.Substring("--port=".Length);
            if (int.TryParse(raw, out int p) && p >= 1024 && p <= 65535)
                return p;
        }
    }

    return null;
}

static int FindAvailablePort(int startPort, int maxAttempts)
{
    int port = startPort;
    for (int i = 0; i < maxAttempts; i++)
    {
        try
        {
            var l = new TcpListener(IPAddress.Loopback, port);
            l.Start();
            l.Stop();
            return port;
        }
        catch
        {
            port++;
        }
    }

    return startPort;
}

static void TryOpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return;
    }
    catch
    {
    }

    try
    {
        Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
    }
    catch
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            MessageBox.Show("Calculadora", $"Servidor iniciado em:{Environment.NewLine}{url}{Environment.NewLine}{Environment.NewLine}Não foi possível abrir o navegador automaticamente.");
    }
}

static string GetDbPath(string[] args)
{
    string? fromArgs = ParseDbPathArg(args);
    if (!string.IsNullOrWhiteSpace(fromArgs))
    {
        string full = Path.GetFullPath(fromArgs);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return full;
    }

    try
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Calculadora");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "calculadora.db");
    }
    catch
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "calculadora.db");
    }
}

static string? ParseDbPathArg(string[] args)
{
    foreach (string a in args)
    {
        if (a.StartsWith("--db-path=", StringComparison.OrdinalIgnoreCase))
            return a.Substring("--db-path=".Length).Trim('"');
    }

    return null;
}

static string? TryLoadHtml()
{
    string? embedded = TryLoadEmbeddedHtml();
    if (!string.IsNullOrWhiteSpace(embedded))
        return embedded;

    string baseDir = AppContext.BaseDirectory;
    for (int i = 0; i < 8; i++)
    {
        string candidate = Path.Combine(baseDir, "calculadora.html");
        if (File.Exists(candidate))
            return File.ReadAllText(candidate, Encoding.UTF8);

        DirectoryInfo? parent = Directory.GetParent(baseDir);
        if (parent is null)
            break;
        baseDir = parent.FullName;
    }

    return null;
}

static string? TryLoadEmbeddedHtml()
{
    Assembly asm = Assembly.GetExecutingAssembly();
    string? name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".calculadora.html", StringComparison.OrdinalIgnoreCase));
    if (name is null)
        return null;

    using Stream? s = asm.GetManifestResourceStream(name);
    if (s is null)
        return null;

    using var reader = new StreamReader(s, Encoding.UTF8);
    return reader.ReadToEnd();
}

sealed record CalcRequest(double N1, double N2, string Op);

sealed record CalcLog(DateTime CreatedUtc, double N1, double N2, string Op, double? Result, bool Ok, string? Error);

sealed record CalcHistoryItem(long Id, DateTime CreatedUtc, double N1, double N2, string Op, double? Result, bool Ok, string? Error);

sealed class CalcDb
{
    private readonly string _connectionString;

    public CalcDb(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                CREATE TABLE IF NOT EXISTS operations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    created_utc TEXT NOT NULL,
                    n1 REAL NOT NULL,
                    n2 REAL NOT NULL,
                    op TEXT NOT NULL,
                    result REAL NULL,
                    ok INTEGER NOT NULL,
                    error TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_operations_created ON operations(created_utc DESC);
                """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task LogAsync(CalcLog log)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO operations (created_utc, n1, n2, op, result, ok, error)
            VALUES ($createdUtc, $n1, $n2, $op, $result, $ok, $error);
            """;
        cmd.Parameters.AddWithValue("$createdUtc", log.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$n1", log.N1);
        cmd.Parameters.AddWithValue("$n2", log.N2);
        cmd.Parameters.AddWithValue("$op", log.Op);
        cmd.Parameters.AddWithValue("$result", (object?)log.Result ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ok", log.Ok ? 1 : 0);
        cmd.Parameters.AddWithValue("$error", (object?)log.Error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<CalcHistoryItem>> GetHistoryAsync(int take)
    {
        var items = new List<CalcHistoryItem>(take);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, created_utc, n1, n2, op, result, ok, error
            FROM operations
            ORDER BY id DESC
            LIMIT $take;
            """;
        cmd.Parameters.AddWithValue("$take", take);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            long id = reader.GetInt64(0);
            string createdUtc = reader.GetString(1);
            double n1 = reader.GetDouble(2);
            double n2 = reader.GetDouble(3);
            string op = reader.GetString(4);
            double? result = reader.IsDBNull(5) ? null : reader.GetDouble(5);
            bool ok = reader.GetInt64(6) == 1;
            string? error = reader.IsDBNull(7) ? null : reader.GetString(7);

            DateTime created = DateTime.TryParse(createdUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.UtcNow;

            items.Add(new CalcHistoryItem(id, created, n1, n2, op, result, ok, error));
        }

        return items;
    }
}

static class ConsoleHost
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    public static void EnsureConsole()
    {
        if (GetConsoleWindow() != IntPtr.Zero)
            return;
        AllocConsole();
    }
}

static class MessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static void Show(string title, string text)
    {
        MessageBoxW(IntPtr.Zero, text, title, 0);
    }
}

static class StartupLog
{
    public static string Write(Exception ex)
    {
        string dir;
        try
        {
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Calculadora", "logs");
            Directory.CreateDirectory(dir);
        }
        catch
        {
            dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
        }

        string file = Path.Combine(dir, $"startup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(file, ex.ToString(), Encoding.UTF8);
        return file;
    }
}
