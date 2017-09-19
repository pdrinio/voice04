using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voice04.Conversaciones
{
    class Nodo
    {
        public int IdNodo;
        public enum TiposNodo { Pregunta, Afirmacion};
        public TiposNodo TipoNodo;
        public string MensajeIda;
        public List<String> PalabrasIdentificativas;
        public List<int> SiguientesNodos;

        public Nodo(int IdNodo, TiposNodo TipoNodo, string MensajeIda, List<string> PalabrasIdentificativas, List<int> SiguientesNodos)
        {
            this.IdNodo = IdNodo;
            this.TipoNodo = TipoNodo;
            this.MensajeIda = MensajeIda;
            this.PalabrasIdentificativas = PalabrasIdentificativas;
            this.SiguientesNodos = SiguientesNodos;
        }
    }
}
