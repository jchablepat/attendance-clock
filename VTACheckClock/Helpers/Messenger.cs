using System;

namespace VTACheckClock.Helpers
{
    // Patrón Messenger o Mediator para comunicación entre componentes de la aplicación.
    public class Messenger
    {
        public static event EventHandler<MessageEventArgs>? MessageReceived;

        // Método para enviar mensajes a los suscriptores. Utiliza un evento para notificar a los suscriptores. 
        public static void Send(string messageType, object data = null!)
        {
            MessageReceived?.Invoke(null, new MessageEventArgs(messageType, data));
        }

        public class MessageEventArgs : EventArgs
        {
            public string MessageType { get; }
            public object Data { get; }

            public MessageEventArgs(string messageType, object data)
            {
                MessageType = messageType;
                Data = data;
            }
        }
    }
}
