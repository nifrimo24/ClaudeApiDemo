// sesion4/QAChainingCasosPrueba.cs
// Demo: Chaining de dos pasos para generación y revisión de casos de prueba
// Paso 1 — Claude genera la lista de casos para CancelarPedido()
// Paso 2 — Claude revisa la lista y señala casos faltantes o inconsistentes
// Comparación: output directo (1 llamada) vs. encadenado (2 llamadas)

using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

var client = new AnthropicClient();

await DemoQA_ChainingCasosPrueba();

async Task DemoQA_ChainingCasosPrueba()
{
    Console.WriteLine("=== DEMO QA: Chaining de dos pasos — Casos de Prueba ===\n");

    // Función objetivo: CancelarPedido en SIMED
    // Tiene suficientes aristas (roles, estados, límite 24 h, excepciones)
    // para que el Paso 2 pueda encontrar huecos relevantes.
    var especificacionFuncion = """
        Función: CancelarPedido(string orderId, string userId, string rol)

        Descripción: Cancela un pedido en SIMED según su estado actual y el rol del usuario.

        Reglas de negocio:
        - Pendiente  (<= 24 h desde creación): cualquier usuario autenticado puede cancelar.
        - Pendiente  (>  24 h desde creación): requiere autorización de supervisor.
        - En proceso : solo puede cancelar un usuario con rol "Supervisor".
        - Enviado    : no cancelable bajo ninguna circunstancia; devuelve error.
        - Entregado  : no cancelable; el sistema redirige al flujo de devolución.
        - orderId inexistente : lanza OrderNotFoundException.
        - userId sin permisos : lanza UnauthorizedAccessException.
        - orderId o userId nulos/vacíos: lanza ArgumentException.
        """;

    // ════════════════════════════════════════════════════════════════
    //  SECCIÓN A — OUTPUT DIRECTO (una sola llamada sin revisión)
    // ════════════════════════════════════════════════════════════════
    Console.WriteLine(new string('═', 60));
    Console.WriteLine("SECCIÓN A — OUTPUT DIRECTO  (1 llamada)");
    Console.WriteLine(new string('═', 60));
    Console.WriteLine();

    var promptDirecto = $"""
        Genera casos de prueba completos para la siguiente función de SIMED.
        Usa IDs reales (ORD-001, usuario01@simed.com) y cubre los escenarios más importantes.

        {especificacionFuncion}

        Por cada caso incluye: ID, Título, Precondiciones, Pasos, Resultado esperado y Datos de prueba.
        """;

    var respDirecta = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 1400,
        Messages = new List<Message> { new Message(RoleType.User, promptDirecto) }
    });

    var outputDirecto = respDirecta.Message.ToString();
    Console.WriteLine(outputDirecto);
    Console.WriteLine($"\n[Tokens directos — entrada: {respDirecta.Usage.InputTokens} · salida: {respDirecta.Usage.OutputTokens}]");

    // ════════════════════════════════════════════════════════════════
    //  SECCIÓN B — OUTPUT ENCADENADO
    //  Paso 1: Generación estructurada y concisa
    // ════════════════════════════════════════════════════════════════
    Console.WriteLine();
    Console.WriteLine(new string('═', 60));
    Console.WriteLine("SECCIÓN B — OUTPUT ENCADENADO  Paso 1: Generación");
    Console.WriteLine(new string('═', 60));
    Console.WriteLine();

    // Prompt enfocado: pide solo el listado, sin desarrollar cada caso completo.
    // El modelo gasta menos tokens y produce un formato fácil de pasar al Paso 2.
    var promptPaso1 = $"""
        Actúa como QA engineer de SIMED. Genera una lista de casos de prueba para:

        {especificacionFuncion}

        Formato de cada caso (una línea por caso):
        TC-NNN | <Título breve> | <Precondición clave> | <Resultado esperado>

        Genera entre 6 y 8 casos. Usa IDs de pedido como ORD-001, roles como "Cliente" y "Supervisor".
        Responde SOLO con la tabla, sin introducción ni cierre.
        """;

    var respPaso1 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 700,
        Messages = new List<Message> { new Message(RoleType.User, promptPaso1) }
    });

    // El output del Paso 1 es la entrada del Paso 2 — núcleo del patrón chaining
    var listaCasos = respPaso1.Message.ToString();
    Console.WriteLine(listaCasos);
    Console.WriteLine($"\n[Tokens Paso 1 — entrada: {respPaso1.Usage.InputTokens} · salida: {respPaso1.Usage.OutputTokens}]");

    // ─────────────────────────────────────────────────────────────────
    //  Paso 2: Revisión crítica — recibe el output del Paso 1
    // ─────────────────────────────────────────────────────────────────
    Console.WriteLine();
    Console.WriteLine(new string('═', 60));
    Console.WriteLine("SECCIÓN B — OUTPUT ENCADENADO  Paso 2: Revisión crítica");
    Console.WriteLine(new string('═', 60));
    Console.WriteLine();

    var promptPaso2 = $"""
        Eres un QA Lead revisando casos de prueba de un compañero para la función CancelarPedido de SIMED.

        Especificación de la función:
        {especificacionFuncion}

        Casos generados por tu compañero:
        ---
        {listaCasos}
        ---

        Analiza la lista e identifica:

        1. CASOS FALTANTES — escenarios contemplados en la especificación que NO tienen caso de prueba.
           Pista: revisa el límite exacto de 24 h, argumentos nulos, cada estado de pedido, cada rol.

        2. CASOS INCONSISTENTES — donde el resultado esperado contradice las reglas de negocio.
           Señala el ID del caso y explica la contradicción.

        3. CASOS REDUNDANTES — dos casos que cubren exactamente el mismo escenario.
           Señala los IDs afectados.

        Responde SIEMPRE en este formato (sin texto fuera de él):

        FALTANTES:
        - <descripción del escenario ausente>

        INCONSISTENTES:
        - <ID>: <descripción del problema con la regla de negocio>

        REDUNDANTES:
        - <ID1> y <ID2>: <motivo de la redundancia>

        VEREDICTO: [COMPLETO | NECESITA_MEJORAS] — <una oración resumen>
        """;

    var respPaso2 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 700,
        Messages = new List<Message> { new Message(RoleType.User, promptPaso2) }
    });

    var revisionCritica = respPaso2.Message.ToString();
    Console.WriteLine(revisionCritica);
    Console.WriteLine($"\n[Tokens Paso 2 — entrada: {respPaso2.Usage.InputTokens} · salida: {respPaso2.Usage.OutputTokens}]");

    // ════════════════════════════════════════════════════════════════
    //  COMPARACIÓN FINAL: directo vs. encadenado
    // ════════════════════════════════════════════════════════════════
    Console.WriteLine();
    Console.WriteLine(new string('═', 60));
    Console.WriteLine("COMPARACIÓN FINAL — Directo vs. Encadenado");
    Console.WriteLine(new string('═', 60));
    Console.WriteLine();

    int tokensEntradaDirecto   = respDirecta.Usage.InputTokens;
    int tokensSalidaDirecto    = respDirecta.Usage.OutputTokens;
    int tokensEntradaEncadenado = respPaso1.Usage.InputTokens + respPaso2.Usage.InputTokens;
    int tokensSalidaEncadenado  = respPaso1.Usage.OutputTokens + respPaso2.Usage.OutputTokens;

    int totalDirecto    = tokensEntradaDirecto   + tokensSalidaDirecto;
    int totalEncadenado = tokensEntradaEncadenado + tokensSalidaEncadenado;

    var veredicto = ExtraerVeredicto(revisionCritica);

    Console.WriteLine($"{"Métrica",-32} {"DIRECTO",10} {"ENCADENADO",12}");
    Console.WriteLine(new string('─', 56));
    Console.WriteLine($"{"Llamadas a la API",-32} {"1",10} {"2",12}");
    Console.WriteLine($"{"Tokens de entrada",-32} {tokensEntradaDirecto,10} {tokensEntradaEncadenado,12}");
    Console.WriteLine($"{"Tokens de salida",-32} {tokensSalidaDirecto,10} {tokensSalidaEncadenado,12}");
    Console.WriteLine($"{"Tokens totales",-32} {totalDirecto,10} {totalEncadenado,12}");
    Console.WriteLine($"{"Revisión crítica incluida",-32} {"No",10} {"Sí",12}");
    Console.WriteLine($"{"Casos faltantes señalados",-32} {"No",10} {"Sí",12}");
    Console.WriteLine($"{"Inconsistencias detectadas",-32} {"No",10} {"Sí",12}");
    Console.WriteLine($"{"Veredicto del revisor",-32} {"—",10} {veredicto,12}");
    Console.WriteLine();
    Console.WriteLine("CONCLUSIONES:");
    Console.WriteLine("  • El output DIRECTO produce casos en una sola llamada,");
    Console.WriteLine("    pero no tiene mecanismo de autocrítica sobre su propia cobertura.");
    Console.WriteLine("  • El ENCADENADO cuesta ~2× en tokens, pero el Paso 2 actúa");
    Console.WriteLine("    como revisor independiente que detecta huecos y contradicciones.");
    Console.WriteLine("  • Patrón útil cuando la calidad de la suite importa más que el costo,");
    Console.WriteLine("    o cuando el contexto de la función es suficientemente complejo");
    Console.WriteLine("    para que una sola pasada no cubra todos los casos límite.");
}

static string ExtraerVeredicto(string texto)
{
    foreach (var linea in texto.Split('\n'))
    {
        var l = linea.Trim();
        if (l.StartsWith("VEREDICTO:", StringComparison.OrdinalIgnoreCase))
            return l["VEREDICTO:".Length..].Trim();
    }
    return "No encontrado";
}
