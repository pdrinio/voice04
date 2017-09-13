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
//using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
//using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
//using Windows.UI.Xaml.Controls.Primitives;
//using Windows.UI.Xaml.Data;
//using Windows.UI.Xaml.Input;
//using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using voice04;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Voice04
{
    public sealed partial class MainPage : Page
    {
        //OBJETOS e INICALIZACIÓN:
        // objetos mqtt (NUGET: Install-Package M2Mqtt -Version 4.3.0)
        private mqtt MiMqtt;

        // presencia
        private Presencia miPresencia;
                
        // mis objetillos de voz
        private SpeechRecognizer speechRecognizer; //continuo
        private SpeechRecognizer speechRecognizerNotas; //toma nota
        private SpeechRecognizer speechRecognizerConversacion; //para conversación
        private IAsyncOperation<SpeechRecognitionResult> recognitionOperation;
        private CoreDispatcher dispatcher;
        private SpeechSynthesizer synthesizer; //habla
        private SpeechRecognitionResult speechRecognitionResult;       
        private enum Estado { Parado, ReconociendoContinuamente, TomandoNota, Fin };
        private Estado miEstado;
        private Estado nextStep;

        private StringBuilder szTextoDictado; //el texto que recoges

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) //cuando llegas
        {
            // MQTT
            MiMqtt = new mqtt();
            MiMqtt.cliente.MqttMsgPublishReceived += cliente_MqttMsgPublishReceivedAsync; //registrarme al evento

            // Presencia
            miPresencia = new Presencia();

            // VOZ
            miEstado = Estado.Parado; //iniciamos parados
            nextStep = Estado.Parado;

            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            //comprobación de si tengo permiso sobre el micrófono; si tengo, inicio el proceso (InitializeRecognizer)
            bool tengoPermiso = await AudioCapturePermissions.RequestMicrophonePermission();
            if (tengoPermiso)
            {
                // inicializa el habla 
                inicializaHabla();

                //escoge castellano (válido para todos los reconocedores)
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;

                // inicializo el reconocedor de gramática compilada
                await InitializeRecognizer(speechLanguage);                

                //// y lanza el control de estados
                await ControlEstado();
            }
            else
            {
                await dime("No tengo acceso al micrófono; cerrando");
                MostrarTexto(txbEstado, "Sin acceso al micrófono");
            }           
        }


        private async Task ControlEstado()
        {
            while(nextStep != Estado.Fin)
               {
                //muestro en el interfaz el estado
                 MostrarTexto(txbEstado, miEstado.ToString());
                 MostrarTexto(txbSiguientePaso, nextStep.ToString());
                
                   if (miEstado == Estado.TomandoNota && nextStep == Estado.TomandoNota)
                   { //para tomar nota, parece que tengo que salir del bucle; ya se invocará desde la toma de nota, para volver
                        break;
                   }
                    else if (miEstado == Estado.Parado && nextStep == Estado.Parado) //funciona para estados: INICIAL, 
                    {
                        nextStep = Estado.ReconociendoContinuamente; //cuando voy a cambiar, modifico el nextStep
                        await reconocerContinuamente();
                    }
                    else if (miEstado == Estado.ReconociendoContinuamente && nextStep == Estado.ReconociendoContinuamente) //funciona para los estados: 'estaba reconociendo, pero no entendí lo que dijo',
                    {
                        await reconocerContinuamente();
                    }
                    else if (miEstado == Estado.ReconociendoContinuamente && nextStep == Estado.TomandoNota)
                    {
                        //se invoca cuando, reconociendo continuamente, el usuario invoca el 'Tomar Nota'
                        await ParaDeReconocerContinuamente();
                        Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
                        await InitializeTomaNota(speechLanguage);
                        await TomaNota();
                    }
                    else if (miEstado == Estado.TomandoNota && nextStep == Estado.ReconociendoContinuamente)
                    {//viene de tomar nota, y sale => va a reconocimiento continuo
                        await ParaTomaNota();
                        await reconocerContinuamente();
                    }                                    
            }                      
        }


        #region MQTT
        public async void cliente_MqttMsgPublishReceivedAsync(object sender, MqttMsgPublishEventArgs e)
        {
            string texto = System.Text.Encoding.UTF8.GetString(e.Message);

            if (texto == mqtt.TiposMensaje.movimiento.ToString())
            {
                MostrarTexto(this.txbConsola, texto); //DEBUG

                if (miPresencia.Esta == Presencia.Contenido.VACIO) //movimiento sin presencia autorizada
                {
                    await dime("Atención: entrada no autorizada");                                  
                }
                else
                    if (miEstado != Estado.TomandoNota && miPresencia.Esta != Presencia.Contenido.VACIO ) {
                    await dime("Hola!!!!");
                    }                                    
            }
        }
        #endregion

        #region GestionDelHabla

        private void inicializaHabla()
        {
            //lanza el habla
            synthesizer = new SpeechSynthesizer();

            VoiceInformation voiceInfo =
         (
           from voice in SpeechSynthesizer.AllVoices
           where voice.Gender == VoiceGender.Female
           select voice
         ).FirstOrDefault() ?? SpeechSynthesizer.DefaultVoice; //escogemos la primera voz femenina

            synthesizer.Voice = voiceInfo;
        }

        private async Task dime(String szTexto)
        {
            try
            {
                // crear el flujo desde el texto
                SpeechSynthesisStream synthesisStream = await synthesizer.SynthesizeTextToStreamAsync(szTexto);

                // ...y lo dice
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    media.AutoPlay = true; //media es un objeto incrustado en el XAML
                    media.SetSource(synthesisStream, synthesisStream.ContentType);
                    media.Play();
                });
                
            }
            catch (Exception e)
            {
                var msg = new Windows.UI.Popups.MessageDialog(e.Message, "Error hablando:");
                await msg.ShowAsync();
            }
        }
        #endregion


        #region ReconocimientoContinuo
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {   //inicialiación del reconocedor 
            if (speechRecognizer != null)
            {
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            try
            {
                //cargar el fichero de la gramática
                string fileName = String.Format("SRGS\\SRGSComandos.xml");
                StorageFile grammaContentFile = await Package.Current.InstalledLocation.GetFileAsync(fileName);

                //inicializamos el objeto reconocedor           
                speechRecognizer = new SpeechRecognizer(recognizerLanguage);

                //activa el feedback al usuario
                speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

                //compilamos la gramática
                SpeechRecognitionGrammarFileConstraint grammarConstraint = new SpeechRecognitionGrammarFileConstraint(grammaContentFile);
                speechRecognizer.Constraints.Add(grammarConstraint);
                SpeechRecognitionCompilationResult compilationResult = await speechRecognizer.CompileConstraintsAsync();

                //si no hubo éxito...
                if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
                {
                    MostrarTexto(txbEstado, "Error compilando la gramática");
                }
                else
                {
                    //MostrarTexto(txbEstado, "Gramática compilada"); DEBUG: no hace falta ya
                    speechRecognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);//damos tiempo a hablar

                }
            }
            catch (Exception e)
            {
                var messageDialog = new Windows.UI.Popups.MessageDialog(e.Message, "Excepción inicializando el  reconocimiento");
                await messageDialog.ShowAsync();
            }

        }

        public async Task reconocerContinuamente()
        {
            try
            {
                //actualizado el estado actual cuando llego (el futuro lo toco cuando decida lo que hacer)
                miEstado = Estado.ReconociendoContinuamente;                

                recognitionOperation = speechRecognizer.RecognizeAsync(); //y utilizamos éste, que no muestra el pop-up
                //limpiaFormulario();  TODO: CASCA aquí al volver de tomar nota

                speechRecognitionResult = await recognitionOperation;

                if (speechRecognitionResult.RawConfidence < 0.5 && speechRecognitionResult.Text != "") //si no ha entendido, pero ha escuchado algo
                {
                    await dime("Creo que no te he entendido");
                    nextStep = Estado.ReconociendoContinuamente; 
                }
                 else
                if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success) //si por el contrario hay match entre gramática y lo escuchado
                {
                    await interpretaResultado(speechRecognitionResult);

                }
                else //aquí nunca llega...
                {
                    MostrarTexto(txbEstado, string.Format("Error de reconocimiento; estado: {0}", speechRecognitionResult.Status.ToString()));
                }
            }

            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine("Me cerraron en el medio del reconocimiento");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            catch (Exception ex2)
            {
                var msg = new Windows.UI.Popups.MessageDialog(ex2.Message, "Error genérico");
                await msg.ShowAsync();
            }
        }

        public async Task ParaDeReconocerContinuamente()
        {
            if (speechRecognizer != null)
            {
                try
                {
                    await speechRecognizer.StopRecognitionAsync();
                    speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                    this.speechRecognizer.Dispose();
                    this.speechRecognizer = null;
                }
                catch (Exception)
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        MostrarTexto(txbEstado, "Problema deteniendo el reconocimiento continuo: ");
                    });
                }
            } else
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    MostrarTexto(txbEstado, "Problema deteniendo el reconocimiento continuo: ");
                });
            }    
        }

        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {//feedback al usuario del estado del reconocimiento de texto
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {                
                txbConsola.Text += args.State.ToString() + Environment.NewLine;
            });

            if (args.State == SpeechRecognizerState.SoundEnded)
            {
                MostrarTexto(txbConsola, args.State.ToString()); //antes cascaba               
            }
        }

        private async Task interpretaResultado(SpeechRecognitionResult recoResult)
        {
            
            if (recoResult.Text.ToString() != "") //si reconoce algo con texto, sea cual sea
            {
                if (recoResult.Status != SpeechRecognitionResultStatus.Success)
                {
                    await dime("Creo que no te he entendido bien"); //aquí nunca ha llegado; TODO: eliminar
                }
                else
                {
                    MostrarTexto(txbTextoReconocido, recoResult.Text + " (" + recoResult.RawConfidence.ToString() + ")"); //presentamos el resultado y su  confianza
                 
                    //lo interpretamos
                    if (recoResult.SemanticInterpretation.Properties.ContainsKey("consulta"))
                    {
                        MostrarTexto(txbConsola, recoResult.SemanticInterpretation.Properties["consulta"][0].ToString());
                        await dime(RespondeALaComunicacion(recoResult.SemanticInterpretation.Properties["consulta"][0].ToString()));
                    }
                }
                if (recoResult.SemanticInterpretation.Properties.ContainsKey("orden"))
                {
                    MostrarTexto(txbConsola, recoResult.SemanticInterpretation.Properties["orden"][0].ToString());
                    await dime(RespondeALaComunicacion(recoResult.SemanticInterpretation.Properties["orden"][0].ToString()));
                    if (recoResult.SemanticInterpretation.Properties["orden"][0].ToString() == "TOMANOTA")
                    {
                        nextStep = Estado.TomandoNota; //pidió tomar nota: el siguiente paso será tomar nota, pues (ver más abajo en qué repercute)
                    }
                }
            }
            else
            { //si no devuelve texto, probablemente hubiera un silencio; volvemos a invocarO
                nextStep = Estado.ReconociendoContinuamente;
            }
        }

        private string RespondeALaComunicacion(string szMensaje)
        {
            switch (szMensaje)
            {
                case "HORA":                    
                    return DateTime.Now.ToString("h:mm");
                case "DIA":
                    return Int32.Parse(DateTime.Now.ToString("dd")) + " de " + DateTime.Now.ToString("MMMM");
                case "TOMANOTA":
                    return "Dí Final Nota para terminar";
                case "BULTO":
                    return "Vamos a crear un bulto";
                case "HOLA":
                    return "Hola, muy buenas!!";
                case "NOMBRE":
                    return "Me llamo Raspi, o al menos es lo que me dijeron";
                case "ESTADO":
                    return "Estoy bien, gracias por preguntar";
                default:
                    return "No sé gestionar tu mensaje";
            }
        }

        private async void BtnLanzaRecoContinuo(object sender, RoutedEventArgs e)
        {
            //inicializamos, y lanzamos el reconocimiento
            Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
            await InitializeRecognizer(speechLanguage);
            await reconocerContinuamente();
        }

        #endregion


        #region TomaNota
        private async Task InitializeTomaNota(Language recognizerLanguage)
        {
            if (speechRecognizerNotas != null)
            {
                //si vengo de una ejecución anterior, hacemos limpieza
                speechRecognizerNotas.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizerNotas.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizerNotas.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizerNotas.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;

                this.speechRecognizerNotas.Dispose();
                this.speechRecognizerNotas = null;
            }

            this.speechRecognizerNotas = new SpeechRecognizer(recognizerLanguage);

            speechRecognizerNotas.StateChanged += SpeechRecognizer_StateChanged; //feedback al usuario

            // en vez de gramática, aplicamos el caso de uso "Dictado"
            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            speechRecognizerNotas.Constraints.Add(dictationConstraint);
            SpeechRecognitionCompilationResult result = await speechRecognizerNotas.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                var messageDialog = new Windows.UI.Popups.MessageDialog(result.Status.ToString(), "Excepción inicializando la toma de notas: ");
                await messageDialog.ShowAsync();
            }

            // nos registramos a los eventos
            speechRecognizerNotas.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed; //no hubo éxito
            speechRecognizerNotas.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated; //o entendió, o llegó basura
            speechRecognizerNotas.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated; //se va alimentando de lo que va llegando para dar feedback
        }

        private async Task TomaNota()
        {
            miEstado = Estado.TomandoNota; //CONTROL ESTADO

            if (speechRecognizerNotas.State == SpeechRecognizerState.Idle)
                {
                    try
                    {                   
                        szTextoDictado = new StringBuilder();                    
                        await speechRecognizerNotas.ContinuousRecognitionSession.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();

                        //cascó, paro
                        miEstado = Estado.Parado;
                        nextStep = Estado.Parado;
                    }
                }
            else
                {
                // hacemos stopAsync para dejar que acabe, ya que no está en reposo...
                   try
                       {
                        await speechRecognizerNotas.ContinuousRecognitionSession.StopAsync();

                        // mostramos lo último entendido
                        MostrarTexto(this.txbTextoReconocido, szTextoDictado.ToString());
                    }
                    catch (Exception exception)
                        {
                            var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                            await messageDialog.ShowAsync();
                        }                    
                }      
        }

        private async Task ParaTomaNota()
        {
            if (this.speechRecognizerNotas != null)
            {
                if (this.speechRecognizerNotas.State != SpeechRecognizerState.Idle)
                {
                    await speechRecognizerNotas.ContinuousRecognitionSession.StopAsync();

                    //paramos el reconocimiento
                    //await speechRecognizerNotas.ContinuousRecognitionSession.CancelAsync();

                    //y actualizamos el estado, y el siguiente paso
      

                    //eliminamos los objetos
                    speechRecognizerNotas.StateChanged -= SpeechRecognizer_StateChanged;
                    speechRecognizerNotas.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                    speechRecognizerNotas.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                    speechRecognizerNotas.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;

                    this.speechRecognizerNotas.Dispose();
                    this.speechRecognizerNotas = null;

                   nextStep = Estado.ReconociendoContinuamente;   
                }
            }

        }

        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                // durante 20 segundos, ha estado callado...                
                if (args.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        MostrarTexto(txbConsola, "Te has pasado de tiempo. Paso a reconocimiento libre");
                        nextStep = Estado.ReconociendoContinuamente; //no parece que llegue nunca aquí
                    });
                }
                else
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        MostrarTexto(txbConsola, "Tomando nota: reconocimiento exitoso");
                        txbConsola.Text = args.Status.ToString();
                        nextStep = Estado.ReconociendoContinuamente;
                    });
                }
            }
        }

        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        { //se han generado resultados, y si la confianza es buena, los presento
            // Caso de que la confianza es buena en lo que ha entendido            
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                szTextoDictado.Append(args.Result.Text + " ");

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                     txbTextoReconocido.Text = szTextoDictado.ToString();
                });


                if (szTextoDictado.ToString().Contains("Final nota"))
                {


                    /* Intento de que casque menos...*/
                    //var szTextoDictadoSinFinalNota = szTextoDictado.ToString().Replace("Final nota", "");

                    //Task t = new Task(async () => await dime("He entendido lo siguiente:"));
                    //t.Start();
                    //t.Wait();

                    //Task t2 = new Task(async () => ParaTomaNota());
                    //t2.Start();
                    //t2.Wait();

                    //miEstado = Estado.ReconociendoContinuamente;
                    //nextStep = Estado.ReconociendoContinuamente;

                    //await ControlEstado();


                    var szTextoDictadoSinFinalNota = szTextoDictado.ToString().Replace("Final nota", "");

                    await ParaTomaNota();

                    await dime("He entendido lo siguiente: " + szTextoDictadoSinFinalNota);

                    miEstado = Estado.ReconociendoContinuamente;
                    nextStep = Estado.ReconociendoContinuamente;

                    await ControlEstado();
                    
                }
            }
            else
            {
                //caso de que la confianza en lo identificado sea baja
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    txbTextoReconocido.Text = szTextoDictado.ToString();
                    string discardedText = args.Result.Text;
                    if (!string.IsNullOrEmpty(discardedText))
                    {
                        discardedText = discardedText.Length <= 25 ? discardedText : (discardedText.Substring(0, 25) + "...");

                        this.txbConsola.Text = "Descartado por baja confianza: " + discardedText;
                    }
                });
            }
        }

        private void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {//según va entendiendo, ir mostrando la información en la UI
            string hypothesis = args.Hypothesis.Text;

            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            string textboxContent = szTextoDictado.ToString() + " " + hypothesis + " ...";            
             dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                txbTextoReconocido.Text = textboxContent;
            });
        }
        #endregion

        #region ElementosVisuales
        private void MostrarTexto (TextBlock txbAActualizar, string TextoAMostrar)
        {
            dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                txbAActualizar.Text = TextoAMostrar;
            });
        }

        private void limpiaFormulario()
        {
            MostrarTexto(this.txbConsola, "");
            MostrarTexto(this.txbEstado, "");
            MostrarTexto(this.txbSiguientePaso, "");
            MostrarTexto(this.txbTextoReconocido, "");             
        }
        #endregion
    }
}
