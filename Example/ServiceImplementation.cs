using System;
using System.Collections.Generic;
using System.Text;

namespace TvdP
{
    public class ServiceImplementation : IService
    {
        public void DoSomething(string text)
        {
            Console.WriteLine($"Doing something with {text}.");
        }
    }
}
