using System.Globalization;

Console.OutputEncoding = System.Text.Encoding.UTF8;
CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

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
        double resultado = opcao switch
        {
            "1" => n1 + n2,
            "2" => n1 - n2,
            "3" => n1 * n2,
            "4" => n2 == 0 ? throw new DivideByZeroException("Divisao por zero nao e permitida.") : n1 / n2,
            "5" => n2 == 0 ? throw new DivideByZeroException("Divisao por zero nao e permitida.") : n1 % n2,
            "6" => Math.Pow(n1, n2),
            _   => throw new InvalidOperationException("Opcao invalida.")
        };

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
