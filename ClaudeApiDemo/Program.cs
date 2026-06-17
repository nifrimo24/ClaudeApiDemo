using ClaudeApiDemo;
using ClaudeApiDemo.Sesion5;

var Reto3 = new Reto3();
var Reto4 = new Reto4();
var Reto5 = new Reto5();


Console.WriteLine("Ejecucion de los retos\n");
int reto = 5;
switch(reto)
{
    case 3:
        Reto3.EjecutarAsync().Wait();
        break;
    case 4:
        Reto4.EjecutarAsync().Wait();
        break;
    case 5:
        Reto5.EjecutarAsync().Wait();
        break;
    default:
        Console.WriteLine("Reto no encontrado");
        break;
}
///No se usa un DataSet de 10 casos en el reto 5 porque da un error de demasiadas peticiones por minutos y se cae por eso lo limite a dos.
