using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

public static class RetoSesion3
{
    private const string SystemPrompt = """
        Eres el asistente de cocina y servicio del Restaurante "El Fogón".
        Tu rol es analizar pedidos y devolver instrucciones precisas para el equipo.

        REGLAS DE NEGOCIO:
        - Horario de cocina: lunes a domingo 12:00–23:00. Último pedido a las 22:30.
        - Modificaciones permitidas: sin cebolla, sin gluten (si el plato lo permite), término de la carne.
        - Modificaciones NO permitidas: cambiar proteína principal, reducir tamaño de porción.
        - Alérgenos críticos: mariscos (línea fría separada), nueces, gluten → alertar al chef.
        - Tiempo estándar: entradas 10 min, platos fuertes 20 min, postres 8 min.
        - Pedidos de más de 6 personas: avisar al sous-chef.
        - Bebidas alcohólicas: no a menores; pedir ID si hay duda.
        - Combos lunes–viernes: entrada + plato + bebida con 15% descuento.
        - Pedido > $200 USD: descuento de cortesía 5% y notificar al gerente.

        FORMATO: Responde ÚNICAMENTE con JSON válido, sin texto adicional ni markdown.
        """;

    public static async Task Ejecutar(AnthropicClient client)
    {
        Console.WriteLine("=== RETO 3: Claude en proyecto real de restaurante ===\n");

        await AnalizarPedido(client, new Pedido(
            Mesa: "A5",
            Personas: 3,
            Items: [
                new("Ceviche Clásico", 2, ""),
                new("Lomo Saltado", 1, "sin cebolla"),
                new("Chicha Morada", 3, "")
            ],
            Notas: ""
        ));

        Console.WriteLine(new string('─', 60) + "\n");

        await AnalizarPedido(client, new Pedido(
            Mesa: "B2",
            Personas: 2,
            Items: [
                new("Arroz con Mariscos", 1, "cliente con alergia severa a mariscos"),
                new("Tiradito de Pescado", 1, "")
            ],
            Notas: "Confirmar con cocina antes de preparar"
        ));

        Console.WriteLine(new string('─', 60) + "\n");

        await RecomendarPlatos(client, restricciones: "vegetariano, sin gluten", presupuesto: 25);
    }

    private record Pedido(string Mesa, int Personas, List<Item> Items, string Notas);
    private record Item(string Nombre, int Cantidad, string Notas);

    private static string LimpiarJson(string texto)
    {
        var t = texto.Trim();
        if (t.StartsWith("```"))
        {
            t = t[3..].TrimStart();
            if (t.StartsWith("json")) t = t[4..];
            var cierre = t.LastIndexOf("```", StringComparison.Ordinal);
            if (cierre >= 0) t = t[..cierre];
        }
        return t.Trim();
    }

    private static async Task AnalizarPedido(AnthropicClient client, Pedido pedido)
    {
        Console.WriteLine($"[Pedido] Mesa {pedido.Mesa} · {pedido.Personas} personas");
        foreach (var i in pedido.Items)
            Console.WriteLine($"  • {i.Cantidad}x {i.Nombre}" + (i.Notas != "" ? $" ({i.Notas})" : ""));
        Console.WriteLine();

        var itemsXml = string.Join("\n    ", pedido.Items.Select(i =>
            $"<item><nombre>{i.Nombre}</nombre><cantidad>{i.Cantidad}</cantidad>" +
            $"<notas>{i.Notas}</notas></item>"));

        var prompt = $$"""
            Analiza este pedido y devuelve el JSON indicado.

            <pedido>
              <mesa>{{pedido.Mesa}}</mesa>
              <personas>{{pedido.Personas}}</personas>
              <items>{{itemsXml}}</items>
              <notas>{{pedido.Notas}}</notas>
            </pedido>

            <instrucciones>
            Devuelve este JSON:
            {
              "alertas": [string],
              "instrucciones_cocina": string,
              "tiempo_estimado_minutos": number,
              "aplica_descuento": boolean,
              "motivo_descuento": string,
              "requiere_supervisor": boolean
            }
            </instrucciones>
            """;

        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 400,
            System = [new SystemMessage(SystemPrompt)],
            Messages = [new Message(RoleType.User, prompt)]
        });

        var json = LimpiarJson(response.Message.ToString());

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var alertas = root.GetProperty("alertas").EnumerateArray()
                .Select(a => a.GetString()!).ToList();
            var instrucciones = root.GetProperty("instrucciones_cocina").GetString();
            var tiempo = root.GetProperty("tiempo_estimado_minutos").GetInt32();
            var descuento = root.GetProperty("aplica_descuento").GetBoolean();
            var supervisor = root.GetProperty("requiere_supervisor").GetBoolean();

            Console.WriteLine(alertas.Count > 0
                ? $"  ALERTAS: {string.Join(" | ", alertas)}"
                : "  Sin alertas");
            Console.WriteLine($"  Cocina: {instrucciones}");
            Console.WriteLine($"  Tiempo estimado: {tiempo} min");
            if (descuento) Console.WriteLine($"  Descuento: {root.GetProperty("motivo_descuento").GetString()}");
            if (supervisor) Console.WriteLine("  → Notificar al supervisor");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  Error al parsear JSON: {ex.Message}");
        }

        Console.WriteLine($"\n  [Tokens: {response.Usage.InputTokens} entrada / {response.Usage.OutputTokens} salida]\n");
    }

    private static async Task RecomendarPlatos(AnthropicClient client, string restricciones, decimal presupuesto)
    {
        Console.WriteLine($"[Recomendación] Restricciones: {restricciones} · Presupuesto: ${presupuesto}/persona\n");

        var prompt = $$"""
            Recomienda 3 platos del Restaurante El Fogón según las preferencias del cliente.

            Formato de respuesta (array JSON, exactamente 3 elementos):
            [
              {"plato": "Ceviche Clásico", "categoria": "entrada", "razon": "fresco y sin gluten", "precio": 12},
              {"plato": "Lomo Saltado", "categoria": "plato fuerte", "razon": "sin mariscos ni nueces", "precio": 18}
            ]

            Cliente:
            - Restricciones: {{restricciones}}
            - Presupuesto por persona: ${{presupuesto}} USD

            Devuelve SOLO el JSON array, sin texto adicional.
            """;

        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 300,
            System = [new SystemMessage(SystemPrompt)],
            Messages = [new Message(RoleType.User, prompt)]
        });

        try
        {
            using var doc = JsonDocument.Parse(LimpiarJson(response.Message.ToString()));
            foreach (var plato in doc.RootElement.EnumerateArray())
            {
                Console.WriteLine($"  • [{plato.GetProperty("categoria").GetString()}] " +
                                  $"{plato.GetProperty("plato").GetString()} — " +
                                  $"${plato.GetProperty("precio").GetDecimal()} " +
                                  $"({plato.GetProperty("razon").GetString()})");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine($"\n  [Tokens: {response.Usage.InputTokens} entrada / {response.Usage.OutputTokens} salida]");
    }
}

