using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace Voice04
{
    public enum Dia
    {
        Hoy,
        Mañana
    }

    class PrevisionAemet
    {
        /* USO:
         *              PrevisionAemet previsionHoy = new PrevisionAemet();
                        PrevisionAemet previsionMañana = new PrevisionAemet();

                        await previsionHoy.ObtenerTemperaturas(Dia.Hoy);
                        await previsionMañana.ObtenerTemperaturas(Dia.Mañana);

            */
        private string szUrlHoy = "http://servizos.meteogalicia.gal/rss/predicion/rssLocalidades.action?idZona=15036&dia=0&request_locale=es";
        private string szUrlMañana = "http://servizos.meteogalicia.gal/rss/predicion/rssLocalidades.action?idZona=15036&dia=+1&request_locale=es";
        private string szUrl = ""; //URL a utilizar, en función de si me llaman para hoy u otro día
        private string XMLDelDia; //XML obtenido para el día requerido

        private int _tMin;
        private int _tMax;

        public int tMin
        {
            get
            {
                return this._tMin;
            }
            set
            {
                _tMin = value;
            }
        }
        public int tMax
        {
            get
            {
                return this._tMax;
            }
            set
            {
                _tMax = value;
            }
        }

        private async Task<String> ObtenerXML(string url)
        {
            try
            {
                using (HttpClient cliente = new HttpClient())
                using (HttpResponseMessage respuesta = await cliente.GetAsync(url))
                using (HttpContent contenido = respuesta.Content)
                {
                    string result = await contenido.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void ExtraerTemperaturas(string XML)
        {
            string[] szLineas = XML.Split(new[] { "\n" }, StringSplitOptions.None);

            foreach (string szLinea in szLineas)
            {
                if (szLinea.Contains("<Concellos:tMax"))
                {
                    this.tMax = Int32.Parse(szLinea.Substring(szLinea.IndexOf("Temperatura máxima") + 20, 2));
                }

                if (szLinea.Contains("<Concellos:tMin"))
                {
                    this.tMin = Int32.Parse(szLinea.Substring(szLinea.IndexOf("Temperatura mínima") + 20, 2));
                }

            }
        }

        public async Task ObtenerTemperaturas(Dia ParaElDiaDe)
        {
            switch (ParaElDiaDe)
            {
                case Dia.Hoy:
                    this.szUrl = this.szUrlHoy;
                    break;
                case Dia.Mañana:
                    this.szUrl = this.szUrlMañana;
                    break;
            }

            XMLDelDia = await ObtenerXML(szUrl).ConfigureAwait(false);
            ExtraerTemperaturas(XMLDelDia);
        }

        public PrevisionAemet()
        {
            tMin = -1;
            tMax = -1;
        }

    }
}
