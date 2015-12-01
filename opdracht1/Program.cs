using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace opdracht1
{
    class Program
    {
        //These will be printed as answers
        public static int counter;
        public static int lineNumber = 1;                                       //used for the list mode
        public static List<Int32[]> bankAccounts = new List<int[]>();
        public static int accountToBlock = -1;

        //An int used for the custom lock
        public static int writeLock = 0;

        //Used for the SHA1 hash
        public static SHA1 sha1 = SHA1.Create();

        //TODO: implement the zoekmodus hash check, the zoekmodus termination, the c# lock


        static void Main(string[] args)
        {
            string readLine = Console.ReadLine();
            string[] splitLine = readLine.Split();

            int lockType = Int32.Parse(splitLine[0]);       //0 for homemade lock, 1 for c# lock
            int lower = Int32.Parse(splitLine[1]);          //inclusive lower bound
            int upper = Int32.Parse(splitLine[2]);          //exclusive upper bound
            int modulus = Int32.Parse(splitLine[3]);        //modulus for the m-test
            int p = Int32.Parse(splitLine[4]);              //amount of threads the program should start
            int u = Int32.Parse(splitLine[5]);              //what mode should the program run, 0, count, 1, list, 2, search
            string h = splitLine[6];                        //the hash used in search mode

            //create a list of threads here
            Thread[] ts = new Thread[p];

            for(int t = 0; t < p; t++)
            {
                List<float> bankAccArray = new List<float>();

                //Create a list of ints for a thread to work on. If there are four threads and lower is 5, an example list is 5, 9, 13, 17. Next is 6, 10, 14, 18
                for (float q = t + lower; q < upper; q += p)
                {
                    bankAccArray.Add(q);
                }

                //create thread t
                Modus modus = new Modus();
                modus.list = bankAccArray;
                modus.lockMode = lockType;
                modus.programMode = u;
                ThreadStart tDelegate = new ThreadStart(modus.TelModus);    //While we do initialize it here, we might overwrite it anyway. This is only done to make the compiler happy!
                //Choose the right mode for the thread to run
                switch(u)                                                   
                {
                    case 0 :
                        modus.mod = modulus;
                        break;
                    case 1 :
                        modus.mod = modulus;
                        tDelegate = new ThreadStart(modus.LijstModus);
                        break;
                    case 2 :
                        tDelegate = new ThreadStart(modus.ZoekModus);
                        modus.hash = h;
                        break;
                }
                ts[t] = new Thread(tDelegate);

                //this terminates the threads when the program closes
                ts[t].IsBackground = true;
            }

            //start threads
            for(int t = 0; t < p; t++)
            {
                ts[t].Start();
            }

            //join threads
            for (int t = 0; t < p; t++)
            {
                ts[t].Join();
            }

            //print answer
            PrintAnswer(u);
            Console.ReadLine();
        }

        class Modus
        {
            public List<float> list;
            public string hash;
            public int mod;
            public int lockMode;
            public int programMode;

            public void TelModus()
            {
                foreach (Int32 num in list)
                {
                    if (MProef(num, mod))
                        customLock.doLock(lockMode, programMode, num);                       //Increment counter
                }
            }

            public void LijstModus()
            {
                foreach (Int32 num in list)
                {
                    if (MProef(num, mod))                                                   //Add to shared list
                    {
                        customLock.doLock(lockMode, programMode, num);
                    }
                }
            }

            public void ZoekModus()
            {
                foreach (Int32 num in list)
                {
                    string numString = num.ToString();
                    byte[] bytes = Encoding.UTF8.GetBytes(numString);
                    byte[] byteHash;

                    byteHash = sha1.ComputeHash(bytes);

                    var sb = new StringBuilder();
                    foreach (byte b in byteHash)
                    {
                        var hex = b.ToString("x2");
                        sb.Append(hex);
                    }

                    //sha1.ComputeHash(bytes);

                    if (sb.ToString() == hash) 
                        customLock.doLock(lockMode, programMode, num);                        
                }
            }
        }

        public static class customLock
        {
            //An object used for the c# lock
            static Object cSLock = new Object();

            public static void doLock(int lockmode, int programMode, int num)
            {
                switch(lockmode)
                {
                    case 0:
                        tas(programMode, num);
                        break;
                    case 1:
                        cSharpLock(programMode, num);
                        break;
                }
            }

            public static void tas(int programMode, int num)
            {
                while (Interlocked.Exchange(ref Program.writeLock, 1) == 1) ;

                switch (programMode)
                {
                    case 0:
                        Program.counter++;
                        break;
                    case 1:
                        int[] toAdd = new int[2];
                        toAdd[0] = Program.lineNumber;
                        toAdd[1] = num;
                        Program.bankAccounts.Add(toAdd);
                        Program.lineNumber++;
                        break;
                    case 2:
                        Console.WriteLine(num);
                        Environment.Exit(0);
                        break;
                }

                Interlocked.Decrement(ref Program.writeLock);               
            }

            public static void cSharpLock(int programMode, int num)
            {
                lock(cSLock)
                {
                    switch (programMode)
                    {
                        case 0:
                            Program.counter++;
                            break;
                        case 1:
                            int[] toAdd = new int[2];
                            toAdd[0] = Program.lineNumber;
                            toAdd[1] = num;
                            Program.bankAccounts.Add(toAdd);
                            Program.lineNumber++;
                            break;
                        case 2:
                            Console.WriteLine(num);
                            Environment.Exit(0);
                            break;
                    }
                }
            }
        }


        public static bool MProef(int number, int modulus)
        {
            List<Int32> list = new List<int>();
            int listLength;
            int listSum = 0;

            while(number != 0)
            {
                list.Add(number % 10);
                number /= 10;
            }
            listLength = list.Count();
            list.Reverse();
            list.ToArray();

            for (int t = 0; t < listLength; t++)
            {
                listSum += list[listLength - t - 1] * (t + 1);
            }

            if (listSum % modulus == 0)
                return true;
            else
                return false;
        }

        public static void PrintAnswer(int mode)
        {
            switch(mode)
            {
                case 0:
                    Console.WriteLine(Program.counter);
                    break;
                case 1:
                    foreach (int[] account in Program.bankAccounts)
                        Console.WriteLine(account[0] + " " + account[1]);
                    break;
                case 2:
                    Console.WriteLine(Program.accountToBlock);
                    break;
            }
        }
    }
}
