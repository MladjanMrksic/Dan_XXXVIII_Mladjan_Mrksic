using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Task_1
{
    class Program
    {
        public static ManualResetEvent manual = new ManualResetEvent(false);
        static Barrier barrier = new Barrier(10);
        static CountdownEvent countdown = new CountdownEvent(2);
        public static CancellationTokenSource cts = new CancellationTokenSource();
        public static CancellationToken ct = cts.Token;
        static readonly object l = new object();
        static List<int> routes = new List<int>();
        static Queue<int> bestRoutes;
        static readonly Random rnd = new Random();
        static string path = @".../.../Routes.txt";
        static StreamReader sr;
        static StreamWriter sw;
        //Creating an instance of semaphore class with initial capacity of 2 and max capacity of 2
        static SemaphoreSlim semaphore = new SemaphoreSlim(2, 2);
        static List<Truck> trucks = new List<Truck>();
        static void Main(string[] args)
        {
            Thread t1 = new Thread(ManagersJob);
            t1.Start();
            Thread t2 = new Thread(RNG);
            Thread.Sleep(100);
            t2.Start();
            t1.Join();
            t2.Join();
            for (int i = 1; i < 11; i++)
            {
                Truck truck = new Truck(i);
                Thread t = new Thread(TruckLoading);
                t.Name = "Thread " + i;
                truck.T = t;
                truck.T.Start(truck);
            }
            Console.ReadLine();
        }
        /// <summary>
        /// ManagersJob method waits for RNG method to generate random routes and then calls for another method to make the selection
        /// </summary>
        public static void ManagersJob()
        {
            //Lock ensures only one thread has access
            lock (l)
            {
                Console.WriteLine("Manager is waiting for routes.");
                //This calls for CancelToken to cancel current execution
                //(In this example it almost never cancels the action because RNG method works so fast that there is no need to cancel)
                cts.CancelAfter(3000);
                //Manager waits for at least 3 seconds before continuing with his job
                Monitor.Wait(l, 3000);
                Console.WriteLine("Manager is chosing the best routes available.");
                //We call a method to do the sorting of routes and make selection of 10 best routes
                LowestDigitsDivisibleByThree();
                foreach (var item in bestRoutes)
                    Console.WriteLine(item);
                Console.WriteLine("Routes are chosen, start loading the trucks.");
            }
        }
        /// <summary>
        /// RNG method generates 1000 random numbers and saves them in text file
        /// </summary>
        public static void RNG()
        {
            //Lock ensures only one thread has access
            lock (l)
            {
                //Creating new instance of StreamWriter and using it to write in file
                sw = new StreamWriter(path);
                using (sw)
                {
                    Console.WriteLine("Generating routes.");
                    for (int i = 0; i < 1000; i++)
                    {
                        //Checking if Cancel is requested before each input
                        if (ct.IsCancellationRequested != true)
                        {
                            int temp = rnd.Next(1, 5000);
                            routes.Add(temp);
                            sw.WriteLine(temp);
                        }
                        //If the cancel is requested the loop will break
                        else
                            break;
                    }
                }
                //Monitor pulse to notify the manager that the RNG method finished it's job
                Monitor.Pulse(l);
            }
        }
        /// <summary>
        /// This method reads from file and selects 10 lowest numbers divisible by three
        /// </summary>
        public static void LowestDigitsDivisibleByThree()
        {
            List<int> temp = new List<int>();
            //Instancing new StreamReader and using it to read from file
            sr = new StreamReader(path);
            using (sr)
            {
                string line;
                //While there are lines to be read, we read them
                while ((line = sr.ReadLine()) != null)
                {
                    //If the number in text file is divisible by 3, we add them to a temporary list
                    if (Convert.ToInt32(line) % 3 == 0)
                        temp.Add(Convert.ToInt32(line));
                }
            }
            //First we sort the list
            temp.Sort();
            //And then add it to an IEnumerable by making sure the numbers are unique and that we only take 10 first (lowest) numbers
            IEnumerable<int> distinctRoutes = temp.Distinct().Take(10);
            //Aftewards we add them to queue
            bestRoutes = new Queue<int>(distinctRoutes);
        }
        /// <summary>
        /// TruckLoading method simulates loading of each individual truck
        /// </summary>
        /// <param name="obj">Object of class truck, representing each thread</param>
        public static void TruckLoading(object obj)
        {
            Truck truck = (Truck)obj;
        up:;
            try
            {
                countdown.Signal();
                countdown.Wait();
            }
            catch
            {
                goto up;
            }
            truck.TruckLoadTime = rnd.Next(500, 5000);
            Console.WriteLine("Truck " + truck.ID + " is loading. Estimated load time is " + truck.TruckLoadTime + ".");
            Thread.Sleep(truck.TruckLoadTime);
            Console.WriteLine("Truck " + truck.ID + " loaded.");
            trucks.Add(truck);
            if (trucks.Count % 2 == 0)
                countdown = new CountdownEvent(2);
            else
                Thread.Sleep(1);
            barrier.SignalAndWait();
            RouteAcquisition(truck);
        }
        public static void RouteAcquisition(object obj)
        {
            Truck truck = (Truck)obj;
            //Each truck is given a route
            truck.TruckRoute = bestRoutes.Dequeue();
            Console.WriteLine("Truck " + truck.ID + " acquired route " + truck.TruckRoute + ".");
            //Each truck is given a random Estimated Time of Arrival
            truck.TruckETA = rnd.Next(500, 5000);
            Thread.Sleep(15);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Truck " + truck.ID + " started the delivery. ETA " + truck.TruckETA + ".");
            Console.ForegroundColor = ConsoleColor.Gray;
            //After all trucks are given a route and ETA, they can be on their way            
            DestinationDelivery(truck);
        }
        public static void DestinationDelivery(object obj)
        {
            Truck truck = (Truck)obj;
            //If truck ETA is longer than 3 seconds, we can conclude they will fail in their delivery
            if (truck.TruckETA > 3000)
            {
                //Thread sleep simulates a length of the journey a truck will make before they are notified of delivery cancelation
                Thread.Sleep(3000);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Truck " + truck.ID + " failed the delivery. Returning to starting point.");
                Console.ForegroundColor = ConsoleColor.Gray;
                //Thread sleep simulated the return journey after the delivery was canceled
                Thread.Sleep(3000);
                Console.WriteLine("Truck " + truck.ID + " returned to the starting point.");
            }
            else
            {
                //If the trucks ETA is lower than 3 seconds we can conclued that the delivery will succeede.
                //Thread sleep simulates truck delivery time
                Thread.Sleep(truck.TruckETA);
                //Unload time is 1.5 times lower than load time
                int unloadTime = Convert.ToInt32(truck.TruckLoadTime / 1.5);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Truck " + truck.ID + " successfuly delivered the load. Unload time estimated at " + unloadTime + ".");
                Console.ForegroundColor = ConsoleColor.Gray;
                //Thread sleep simulated unload time
                Thread.Sleep(unloadTime);
                Console.WriteLine("Truck " + truck.ID + " unloaded.");
            }
        }
    }
    /// <summary>
    /// Class truck was made so that each thread has an object that will hold relevant data
    /// Also, each Truck has a specific thread which represents their delivery
    /// </summary>
    public class Truck
    {
        public Thread T;
        public int ID;
        public int TruckRoute;
        public int TruckLoadTime;
        public int TruckETA;
        public Truck(int id)
        {
            ID = id;
        }
    }
}
