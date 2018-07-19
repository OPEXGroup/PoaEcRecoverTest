using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ContractCreationTest;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace PoaEcRecoverTest
{
    public static class Program
    {
        private const string ContractName = "Test";

        private const string SokolNodeUrl = "https://sokol-trace.poa.network/";

        /**
         * NB: Id!
         */
        private const Chain SokolNetworkId = (Chain)77;

        private const string Address = "0xCAE25d8cDc94136d348469C13B221412543f3711";
        private const string PrivateKey = "897af311f21765ca83fd6650ec1537c8bb783c63f57ac405796db7934f5c0cae";
        private const string ContractOutputFolder = "ContractOutput";

        private static async Task Main(string[] args)
        {
            try
            {
                CompileContract(args);

                var web3 = CreateWeb3();
                var contract = await DeployContractAsync(web3);

                var function = contract.GetFunction("callEcRecover");
                var message = new Sha3Keccack().CalculateHash(Encoding.ASCII.GetBytes("Hello"));
                var messageSigner = new MessageSigner();
                var ecdsaSignature = messageSigner.SignAndCalculateV(message, PrivateKey);


                var address = await function.CallAsync<string>(new object[]
                {
                    message,
                    ecdsaSignature.V[0],
                    ecdsaSignature.R,
                    ecdsaSignature.S
                });
                Log($"Received: {address.Substring(2).ToLowerInvariant()}; Our: {Address.Substring(2).ToLowerInvariant()} Correct: {string.Equals(address.Substring(2), Address.Substring(2), StringComparison.OrdinalIgnoreCase)}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void CompileContract(string[] args)
        {
            var solcPath = Path.GetFullPath(GetSolcPath(args));
            if (!File.Exists(solcPath))
                throw new Exception($"solc in unavailable ({solcPath})");
            Log($"Using compiler {solcPath}");
            var sourcePath = Path.GetFullPath(GetContractSourcePath());
            Log($"Using contract {sourcePath}");

            var compilerArgs = $"--bin --abi --overwrite --optimize -o {ContractOutputFolder} {sourcePath}";
            Log($"Running compiler with args {compilerArgs}");

            var process = Process.Start(solcPath, compilerArgs);
            if (process == null)
            {
                throw new Exception("Failed to start compiler");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new Exception("Failed to compile contract");

            Log("Contract compiled");
        }

        private static Web3 CreateWeb3()
        {
            var account = new Account(PrivateKey, SokolNetworkId);
            var web3 = new Web3(account, SokolNodeUrl)
            {
                Client =
                {
                    // Log all requests and responses
                    OverridingRequestInterceptor = new LoggingRequestInterceptor()
                }
            };
            return web3;
        }

        private static async Task<Contract> DeployContractAsync(Web3 web3)
        {
            var contractAbi = File.ReadAllText($"{ContractOutputFolder}/{ContractName}.abi");
            var contractBin = File.ReadAllText($"{ContractOutputFolder}/{ContractName}.bin");

            // 20 GWei. Feel free to change
            var gasPrice = new HexBigInteger(new BigInteger(30_000_000_000));
            // Contract creation transactions have zero value
            var value = new HexBigInteger(new BigInteger(0));

            var gasAmount = await web3.Eth.DeployContract.EstimateGasAsync(contractAbi, contractBin, Address);
            Log($"Estimated gas amount for deploy: {gasAmount.Value}");

            var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                abi: contractAbi,
                contractByteCode: contractBin,
                from: Address,
                gas: gasAmount,
                gasPrice: gasPrice,
                value: value);
            var contractAddress = receipt.ContractAddress;
            Log($"Contract deployed at address {contractAddress}");

            var contract = web3.Eth.GetContract(contractAbi, contractAddress);
            return contract;
        }

        private static string GetSolcPath(string[] args)
        {
            // We can pass solc executable path as first arg
            if (args.Length > 1)
            {
                var path = args[1];
                if (File.Exists(path))
                    return path;
            }

            // MSVS hack (be compatible with 'netcoreapp2.1' in build path)
            const string firstTry = "../tools/win32/solc.exe";
            if (File.Exists(firstTry))
                return firstTry;

            return "../" + firstTry;
        }

        private static string GetContractSourcePath()
        {
            // MSVS hack (be compatible with 'netcoreapp2.1' in build path)
            const string firstTry = "../contract/Test.sol";
            if (File.Exists(firstTry))
                return firstTry;

            return "../" + firstTry;
        }

        private static void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
