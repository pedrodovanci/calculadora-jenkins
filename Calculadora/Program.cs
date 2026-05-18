using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;
CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

if (args.Any(a => string.Equals(a, "--server", StringComparison.OrdinalIgnoreCase)))
{
    await RunServerAsync();
    return;
}

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

static async Task RunServerAsync()
{
    const int port = 5005;
    string prefix = $"http://localhost:{port}/";

    using var listener = new HttpListener();
    listener.Prefixes.Add(prefix);
    listener.Start();

    Console.WriteLine($"Servidor iniciado em {prefix}");
    Console.WriteLine("Abra no navegador: http://localhost:5005/");
    Console.WriteLine("Para parar: Ctrl+C");

    while (listener.IsListening)
    {
        HttpListenerContext ctx = await listener.GetContextAsync();
        _ = Task.Run(() => HandleRequestAsync(ctx));
    }
}

static async Task HandleRequestAsync(HttpListenerContext ctx)
{
    try
    {
        string path = ctx.Request.Url?.AbsolutePath ?? "/";

        if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase))
        {
            string? html = TryLoadHtml();
            if (html is null)
            {
                await WriteTextAsync(ctx, 200, "text/plain; charset=utf-8", "Arquivo calculadora.html não encontrado.");
                return;
            }

            await WriteTextAsync(ctx, 200, "text/html; charset=utf-8", html);
            return;
        }

        if (string.Equals(path, "/api/calc", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
            string body = await reader.ReadToEndAsync();

            CalcRequest? req = JsonSerializer.Deserialize<CalcRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (req is null || string.IsNullOrWhiteSpace(req.Op))
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Requisição inválida." });
                return;
            }

            try
            {
                double result = Calcular(req.Op, req.N1, req.N2);
                await WriteJsonAsync(ctx, 200, new { ok = true, result });
            }
            catch (Exception ex)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
            }

            return;
        }

        await WriteTextAsync(ctx, 404, "text/plain; charset=utf-8", "Não encontrado.");
    }
    catch
    {
        try
        {
            await WriteTextAsync(ctx, 500, "text/plain; charset=utf-8", "Erro interno.");
        }
        catch
        {
        }
    }
    finally
    {
        try
        {
            ctx.Response.OutputStream.Close();
        }
        catch
        {
        }
    }
}

static string? TryLoadHtml()
{
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

static async Task WriteTextAsync(HttpListenerContext ctx, int statusCode, string contentType, string text)
{
    byte[] bytes = Encoding.UTF8.GetBytes(text);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = contentType;
    ctx.Response.ContentLength64 = bytes.Length;
    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
}

static async Task WriteJsonAsync(HttpListenerContext ctx, int statusCode, object payload)
{
    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.ContentLength64 = bytes.Length;
    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
}

sealed record CalcRequest(double N1, double N2, string Op);
