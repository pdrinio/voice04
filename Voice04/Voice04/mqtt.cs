using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Voice04
{
    class mqtt
    {
        private string MqttServidor = "192.168.4.15";
        private string MqttTopic = "/pir/entrada";
        public MqttClient cliente;
        public enum TiposMensaje { movimiento, parado };
        public TiposMensaje Mensaje;


        public mqtt()
        {
            
            cliente = new MqttClient(MqttServidor);

            Mensaje = new TiposMensaje();

        //cliente.MqttMsgPublishReceived += cliente_MqttMsgPublishReceived; //registrarme al evento

            string clientId = Guid.NewGuid().ToString(); //conectar
            cliente.Connect(clientId);

            // suscribir al "canal", QoS2
            cliente.Subscribe(new string[] { MqttTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }
    }
}
