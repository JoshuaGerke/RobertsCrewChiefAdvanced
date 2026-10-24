using System;

namespace CrewChiefV4
{
    [Serializable]
    class GameDataReadException : Exception
    {
        public GameDataReadException()
        {
        }
        public GameDataReadException(String message) : base(message)
        {
        }
        public GameDataReadException(String message, Exception inner) : base(message, inner)
        {
        }
    }
}
