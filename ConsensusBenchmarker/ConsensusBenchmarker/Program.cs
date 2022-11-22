﻿// See https://aka.ms/new-console-template for more information

using ConsensusBenchmarker.Communication;

namespace ConsensusBenchmarker;

class Program
{
    static async Task Main(string[] args)
    {
        CommunicationModule communicationModule = new CommunicationModule(1);
        await communicationModule.WaitInstruction();

        //var communicationTask = new Task(async () =>
        //{
        //    await communicationModule.WaitInstruction();
        //});

        //Parallel.Invoke(async () => await communicationModule.WaitInstruction());

        //Console.WriteLine("Task started");
    }
}
