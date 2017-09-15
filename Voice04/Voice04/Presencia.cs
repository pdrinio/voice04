using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voice04
{
    class Presencia
    {
        public enum Habitantes { Pa, Ma, Cris, Pablo};
        public List<Habitantes> Personas; 
     

        public Presencia()
        {           
            Personas.Add(Habitantes.Pa);
            Personas.Add(Habitantes.Ma);
            Personas.Add(Habitantes.Cris);
            Personas.Add(Habitantes.Pablo);
        }
    }
}
