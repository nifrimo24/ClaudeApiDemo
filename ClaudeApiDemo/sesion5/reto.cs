using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

public static class RetoSesion5
{
    private const string SystemPromptV1 = """
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


    private const string SystemPromptV2 = """
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

        ANTES DE RESPONDER:
        1. Lee el campo <notas> con cuidado: ahí puede venir el día de la semana, la hora del
           pedido o el total estimado en USD. Esos datos son necesarios para aplicar las reglas
           de horario, combos y descuentos — no los ignores solo porque no están en un campo
           dedicado.
        2. Recorre la lista de 9 reglas de negocio una por una y decide explícitamente si cada
           una aplica a este pedido. No omitas ninguna por asumir que "no aplica".
        3. Si detectas una modificación NO permitida (cambiar proteína, reducir porción), recházala
           en "instrucciones_cocina" y agrégala a "alertas" — no la aceptes en silencio.
        4. Si el pedido llega después de las 22:30 o fuera del horario 12:00–23:00, la alerta
           principal debe ser que la cocina está cerrada, antes que cualquier otro detalle.

        FORMATO: Responde ÚNICAMENTE con JSON válido, sin texto adicional ni markdown. El JSON
        debe tener exactamente estas claves: alertas, instrucciones_cocina, tiempo_estimado_minutos,
        aplica_descuento, motivo_descuento, requiere_supervisor.
        """;

    private record Caso(
        string Id,
        string Foco,
        string Mesa,
        int Personas,
        List<(string Nombre, int Cantidad, string Notas)> Items,
        string Notas);

    public static async Task Ejecutar(AnthropicClient client)
    {
        Console.WriteLine("=== RETO 5: Eval pipeline — prompt de análisis de pedidos (El Fogón) ===\n");

        var dataset = ConstruirDataset();

        Console.WriteLine($"Dataset: {dataset.Count} casos representativos\n");

        Console.WriteLine("─── Ejecutando VERSIÓN A (prompt actual) ───");
        var scoresA = await EvaluarVersion(client, SystemPromptV1, dataset);

        Console.WriteLine("\n─── Ejecutando VERSIÓN B (prompt mejorado) ───");
        var scoresB = await EvaluarVersion(client, SystemPromptV2, dataset);

        var promedioA = scoresA.Average();
        var promedioB = scoresB.Average();
        var delta = promedioB - promedioA;

        Console.WriteLine("\n══════════════════════ RESULTADOS ══════════════════════");
        Console.WriteLine($"Versión A (actual)   — promedio: {promedioA:F2}/10  (n={scoresA.Count})");
        Console.WriteLine($"Versión B (mejorada) — promedio: {promedioB:F2}/10  (n={scoresB.Count})");
        Console.WriteLine($"Δ = {(delta >= 0 ? "+" : "")}{delta:F2} puntos" +
                           (delta > 0 ? " ↑ mejora" : delta < 0 ? " ↓ empeora" : " (sin cambio)"));
        Console.WriteLine("══════════════════════════════════════════════════════════");
    }

    private static List<Caso> ConstruirDataset() => new()
    {
        new Caso("C01", "Caso control: pedido simple, sin reglas especiales que aplicar.",
            "A1", 2,
            [("Ceviche Clásico", 1, ""), ("Lomo Saltado", 1, ""), ("Chicha Morada", 2, "")],
            ""),

        new Caso("C02", "Alergia crítica a mariscos: debe alertar al chef y pedir línea fría separada.",
            "B3", 2,
            [("Arroz con Mariscos", 1, "cliente con alergia severa a mariscos"), ("Limonada", 2, "")],
            "Confirmar con cocina antes de preparar"),

        new Caso("C03", "Modificación permitida (sin gluten): debe aceptarse sin alertas de rechazo.",
            "C2", 1,
            [("Lomo Saltado", 1, "sin gluten")],
            ""),

        new Caso("C04", "Modificación NO permitida (cambiar proteína): debe rechazarse explícitamente.",
            "D4", 2,
            [("Lomo Saltado", 1, "cambiar la carne de res por pollo")],
            ""),

        new Caso("C05", "Grupo de más de 6 personas: debe avisar al sous-chef.",
            "E1", 8,
            [("Ceviche Clásico", 4, ""), ("Lomo Saltado", 4, ""), ("Chicha Morada", 8, "")],
            ""),

        new Caso("C06", "Bebida alcohólica con duda de edad: debe pedir identificación.",
            "F2", 1,
            [("Pisco Sour", 1, "")],
            "El cliente se ve muy joven, el mesero tiene dudas sobre su edad"),

        new Caso("C07", "Combo lunes-viernes (entrada+plato+bebida): debe aplicar 15% de descuento. " +
                          "El día/hora vienen en las notas, no en un campo dedicado.",
            "G5", 2,
            [("Ceviche Clásico", 1, ""), ("Lomo Saltado", 1, ""), ("Chicha Morada", 1, "")],
            "Pedido realizado un miércoles a las 13:00"),

        new Caso("C08", "Pedido > $200 USD: debe aplicar 5% de descuento de cortesía y notificar al gerente. " +
                          "El total estimado viene en las notas, no en un campo dedicado.",
            "H1", 6,
            [("Lomo Saltado", 4, ""), ("Tiradito de Pescado", 2, ""), ("Chicha Morada", 6, "")],
            "Total estimado del pedido: $230 USD"),

        new Caso("C09", "Pedido fuera de horario (después de las 22:30): la alerta principal debe ser " +
                          "que la cocina está cerrada. La hora viene en las notas, no en un campo dedicado.",
            "I3", 2,
            [("Postre Suspiro", 2, "")],
            "Pedido realizado a las 22:45"),

        new Caso("C10", "Combinación de reglas: grupo de 7 + alergia a nueces + bebida alcohólica sin duda " +
                          "de edad (cliente claramente adulto). Debe avisar al sous-chef y alertar la alergia, " +
                          "pero NO debe pedir ID innecesariamente.",
            "J7", 7,
            [("Postre Suspiro", 2, "sin nueces, alergia severa"), ("Pisco Sour", 7, "todos mayores de 40 años")],
            "")
    };

    private static async Task<List<double>> EvaluarVersion(AnthropicClient client, string systemPrompt, List<Caso> dataset)
    {
        var scores = new List<double>();

        foreach (var caso in dataset)
        {
            var pedidoXml = ConstruirPedidoXml(caso);

            var prompt = $$"""
                Analiza este pedido y devuelve el JSON indicado.

                {{pedidoXml}}

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

            var respuesta = await EnviarConReintento(client, new MessageParameters
            {
                Model = AnthropicModels.Claude45Haiku,
                MaxTokens = 400,
                System = [new SystemMessage(systemPrompt)],
                Messages = [new Message(RoleType.User, prompt)]
            });

            var output = respuesta.Message.ToString();

            var puntuacion = await Calificar(client, caso, pedidoXml, output);
            scores.Add(puntuacion);

            Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] [{caso.Id}] {caso.Foco[..Math.Min(55, caso.Foco.Length)]}... → {puntuacion:F1}/10");
        }

        return scores;
    }

    private static string ConstruirPedidoXml(Caso caso)
    {
        var itemsXml = string.Join("\n    ", caso.Items.Select(i =>
            $"<item><nombre>{i.Nombre}</nombre><cantidad>{i.Cantidad}</cantidad>" +
            $"<notas>{i.Notas}</notas></item>"));

        return $"""
            <pedido>
              <mesa>{caso.Mesa}</mesa>
              <personas>{caso.Personas}</personas>
              <items>{itemsXml}</items>
              <notas>{caso.Notas}</notas>
            </pedido>
            """;
    }

    private static async Task<double> Calificar(AnthropicClient client, Caso caso, string pedidoXml, string output)
    {
        var promptGrader = $$"""
            Eres un evaluador experto que califica si un asistente de cocina de restaurante
            siguió correctamente sus reglas de negocio.

            REGLAS DE NEGOCIO QUE DEBÍA APLICAR:
            - Horario de cocina: lunes a domingo 12:00–23:00. Último pedido a las 22:30.
            - Modificaciones permitidas: sin cebolla, sin gluten, término de la carne.
            - Modificaciones NO permitidas: cambiar proteína principal, reducir porción.
            - Alérgenos críticos: mariscos, nueces, gluten → alertar al chef.
            - Pedidos de más de 6 personas: avisar al sous-chef.
            - Bebidas alcohólicas: pedir ID solo si hay duda razonable de edad.
            - Combos lunes–viernes (entrada+plato+bebida): 15% descuento.
            - Pedido > $200 USD: 5% descuento de cortesía y notificar al gerente.

            FOCO ESPECÍFICO DE ESTE CASO: {{caso.Foco}}

            Pedido recibido por el asistente:
            {{pedidoXml}}

            Respuesta del asistente:
            {{output}}

            Proporciona tu evaluación EN TEXTO PLANO, sin markdown ni encabezados en negrita,
            siguiendo este formato exacto y nada más (cada campo en una sola línea corta):
            Fortalezas: <máximo 12 palabras>
            Debilidades: <máximo 12 palabras>
            Razonamiento: <una sola oración corta, citando si aplicó o no la regla del foco>
            Puntuación: <número entre 1 y 10>
            """;

        var grading = await EnviarConReintento(client, new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 300,
            Messages = [new Message(RoleType.User, promptGrader)]
        });

        var textoGrading = grading.Message.ToString();
        return ExtraerPuntuacion(textoGrading);
    }

    private static DateTime _ultimaLlamadaUtc = DateTime.MinValue;
    private static readonly TimeSpan EspacioMinimo = TimeSpan.FromSeconds(13);

    private static async Task<MessageResponse> EnviarConReintento(AnthropicClient client, MessageParameters parametros)
    {
        var transcurrido = DateTime.UtcNow - _ultimaLlamadaUtc;
        if (transcurrido < EspacioMinimo)
            await Task.Delay(EspacioMinimo - transcurrido);

        const int maxIntentos = 6;
        var espera = TimeSpan.FromSeconds(15);

        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            try
            {
                var respuesta = await client.Messages.GetClaudeMessageAsync(parametros);
                _ultimaLlamadaUtc = DateTime.UtcNow;
                return respuesta;
            }
            catch (Exception ex) when (intento < maxIntentos &&
                                        (ex is RateLimitsExceeded or HttpRequestException or TaskCanceledException or IOException))
            {
                Console.WriteLine($"    [{ex.GetType().Name} — esperando {espera.TotalSeconds:F0}s antes de reintentar (intento {intento}/{maxIntentos})]");
                await Task.Delay(espera);
                espera += TimeSpan.FromSeconds(5);
            }
        }

        var ultima = await client.Messages.GetClaudeMessageAsync(parametros);
        _ultimaLlamadaUtc = DateTime.UtcNow;
        return ultima;
    }

    private static readonly Regex PuntuacionRegex =
        new(@"Puntuaci[oó]n[^\d]{0,15}(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);

    private static double ExtraerPuntuacion(string texto)
    {
        var match = PuntuacionRegex.Match(texto);
        if (match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var n))
            return n;
        return 5.0;
    }
}
