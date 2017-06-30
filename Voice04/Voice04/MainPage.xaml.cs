using System.Threading.Tasks;
using Windows.Storage;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.Media.SpeechSynthesis;
using System.Diagnostics;
using System.Text;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// La plantilla de elemento Página en blanco está documentada en http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Voice04
{
    /// <summary>
    /// Página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //mis objetillos
        private SpeechRecognizer speechRecognizer;
        private SpeechRecognizer speechRecognizerNotas;
        private IAsyncOperation<SpeechRecognitionResult> recognitionOperation;
        private CoreDispatcher dispatcher;
        private SpeechSynthesizer synthesizer;
        private SpeechRecognitionResult speechRecognitionResult;       
        private enum Estado { Parado, ReconociendoContinuamente, TomandoNota };        
        private Estado miEstado;
        private Estado nextStep;

        private StringBuilder szTextoDictado; //el texto que recoges

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) //cuando llegas
        {
            miEstado = Estado.Parado; //iniciamos parados
            nextStep = Estado.Parado;

            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            //comprobación de si tengo permiso sobre el micrófono; si tengo, inicio el proceso (InitializeRecognizer)
            bool tengoPermiso = await AudioCapturePermissions.RequestMicrophonePermission();
            if (tengoPermiso)
            {
                // lanza el habla 
                inicializaHabla();

                //escoge castellano (válido para todos los reconocedores)
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;

                // inicializo los dos reconocedores (el de gramática compilada, y el contínuo de las notas)                
                await InitializeRecognizer(speechLanguage);
                await InitializeTomaNota(speechLanguage);

                //da la bienvenida


                //// y lanza EL TOMA NOTA, para saber cuándo me hace la llamada
                TomaNota();

            }
            else
            {
                tbEstadoReconocimiento.Visibility = Visibility.Visible;
                tbEstadoReconocimiento.Text = "Sin acceso al micrófono";
            }
        }
    }
}
