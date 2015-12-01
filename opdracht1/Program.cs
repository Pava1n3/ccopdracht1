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
        //creat an appropriate lock and modus. 

        //These will be printed as answers
        public static int counter;                                              //the counter for counting mode
        //public static int lineNumber = 1;                                       //used for the list mode
        public static List<Int32[]> bankAccounts = new List<int[]>();           //store the accounts returned by list mode
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

            //Create the lock to be used by the program. The lock instance also creates an instance of ProgramModus inside itself to write away ansers
            customLock programLock;
            if (lockType == 0)
                programLock = new tasLock(u);
            else 
                programLock = new cSharpLock(u);
            

            //create a list of threads here
            Thread[] ts = new Thread[p];

            //The maximum amount of threads we can make based on the lower and upper bounds
            int maxThreads = Math.Min(upper - lower, p);
            int leftover;
            //if p is larger than the bounds, leftover will screw up the assigning of numbers
            if(p < upper - lower)
                leftover = (upper - lower) % p;
            else 
                leftover = -1;
            //used to divide the remainder of (upper - lower) % p
            int lastUpper = lower;

            for(int t = 0; t < maxThreads; t++)
            {
                //Calculate the range of numbers a thread has to handle
                int segmentReach = Math.Max(1, (upper - lower) / p);
                int[] segmentBounds = new int[2];

                segmentBounds[0] = lastUpper;

                if (maxThreads - (t + 1) < leftover)
                    segmentBounds[1] = lower + (t + 1) * (segmentReach) + 1;
                else
                    segmentBounds[1] = lower + (t + 1) * segmentReach;

                lastUpper = segmentBounds[1];

                //create thread t
                DoeMProef dmp = new DoeMProef();
                dmp.segment = segmentBounds;  //the bounds the thread has to handle
                dmp.programLock = programLock;
                dmp.mod = modulus;

                //Create the right method, doMP for lijst and tel, doHashProef for zoek
                ThreadStart tDelegate;
                if (u == 2)
                {
                    dmp.hash = h;
                    tDelegate = new ThreadStart(dmp.doHashProef);
                }
                else
                    tDelegate = new ThreadStart(dmp.doMP);   


                ts[t] = new Thread(tDelegate);

                //this terminates the threads when the program closes
                ts[t].IsBackground = true;
            }

            //start threads
            //for(int t = 0; t < p; t++)
            for(int t = 0; t < maxThreads; t++ )
            {
                ts[t].Start();
            }

            //join threads
            //for (int t = 0; t < p; t++)
            for (int t = 0; t < maxThreads; t++)
            {
                ts[t].Join();
            }

            //print answer if the zoek modus hasn't found anything
            //if (u == 2 && accountToBlock == -1)
                //Console.WriteLine(-1);
            PrintAnswer(u);
            Console.ReadLine();
        }

        class DoeMProef
        {
            public int[] segment;     //what reach of numbers this thread has to handle
            public string hash;
            public int mod;

            public customLock programLock;

            //Do the m proef, call on the current lock to handle the answer if it succeeds the m proef
            public void doMP()
            {
                for(int i = segment[0]; i < segment[1]; i++)
                {
                    if (MProef(i, mod))
                        programLock.doLock(i);
                }
            }

            public void doHashProef()
            {
                for (int i = segment[0]; i < segment[1]; i++)
                {
                    if (MProef(i, mod))
                    {
                        string numString = i.ToString();
                        byte[] bytes = Encoding.UTF8.GetBytes(numString);
                        //byte[] bytes = BitConverter.GetBytes(i);
                        byte[] byteHash;

                        byteHash = sha1.ComputeHash(bytes);

                        string hashString = "";
                        //hashString = byteHash.ToString();


                        //for (int j = 0; j < byteHash.Length; j++)
                        //{
                        //    hashString += byteHash[j].ToString();
                        //}

                        var sb = new StringBuilder(byteHash.Length * 2);
                        foreach (byte b in byteHash)
                        {
                            var hex = b.ToString("x2");
                            sb.Append(hex);
                        }

                        hashString = sb.ToString();

                        //sha1.ComputeHash(bytes);

                        if (hashString == hash)
                            programLock.doLock(i);
                    }
                }
            }
        }

        //These are the locks
        public abstract class customLock
        {
            protected ProgramModus modus;

            public customLock(int programMode)
            {
                switch (programMode)
                {
                    case 0:
                        modus = new TelModus();
                        break;
                    case 1:
                        modus = new LijstModus();
                        break;
                    case 2:
                        modus = new ZoekModus();
                        break;
                }
            }

            public abstract void doLock(int num);

        }

        public class tasLock : customLock
        {
            public tasLock(int programMode) : base(programMode)
            {}

            //do the locking work, and call on the modus instance to write away the outcome
            public override void doLock(int num)
            {
                while (Interlocked.Exchange(ref Program.writeLock, 1) == 1) ;

                modus.writeAnswer(num);

                Interlocked.Decrement(ref Program.writeLock);              
            }
        }

        public class cSharpLock : customLock
        {
            public cSharpLock(int programMode) : base(programMode)
            {}
            
            //An object used for the c# lock
            static Object cSLock = new Object();

            //do the locking work, and call on the modus instance to write away the outcome
            public override void doLock(int num)
            {
                lock (cSLock)
                {
                    modus.writeAnswer(num);
                }
            }
        }

        //These classes will write the correct answer, so counter++, list.add or return the hashed account
        public abstract class ProgramModus
        {
            public abstract void writeAnswer(int num);
        }

        public class TelModus : ProgramModus
        {
            public override void writeAnswer(int num)
            {
                Program.counter++;
            }
        }

        public class LijstModus : ProgramModus
        {
            public override void writeAnswer(int num)
            {
                int[] toAdd = new int[2];

                toAdd[0] = Program.bankAccounts.Count + 1;
                toAdd[1] = num;
                Program.bankAccounts.Add(toAdd);
            }
        }

        public class ZoekModus : ProgramModus
        {
            public override void writeAnswer(int num)
            {
                //Console.WriteLine(num);
                Program.accountToBlock = num;
                PrintAnswer(2);
                Environment.Exit(0);
            }
        }


        public static bool MProef(int number, int modulus)
        {
            int numberCount = 1;
            int num = number;
            int sum = 0;

            while(num > 0)
            {
                sum += numberCount * (num % 10);

                numberCount++;
                num /= 10;
            }

            if (sum % modulus == 0)
                return true;

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
                    Environment.Exit(0);
                    break;
            }
        }
    }
}
