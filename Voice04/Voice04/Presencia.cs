using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voice04
{
    class Presencia
    {
        public enum Contenido
        {
            VACIO = 0,
            M = 1,
            F = 1,
            FC = 3,
            MC = 3,
            ALL = 4 };

        public Contenido Esta;        

        public Presencia()
        {
            Esta = Contenido.ALL;
        }
    }
}
