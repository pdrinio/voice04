using System.Collections.Generic;

namespace Voice04
{
    public class Presencia
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

        public void Add(Habitantes HabitanteEntrante)
        {
            if (!Personas.Contains(HabitanteEntrante))
                Personas.Add(HabitanteEntrante);                  
        }

        public void Remove(Habitantes HabitanteSaliente)
        {
            if (Personas.Contains(HabitanteSaliente))
                Personas.Remove(HabitanteSaliente);
        }

        public int Contains()
        {
            return Personas.Count();
        }
    }
}
