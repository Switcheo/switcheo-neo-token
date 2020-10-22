using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace NEP5
{
    // [Features(ContractFeatures.HasStorage)]
    public class NEP5 : SmartContract
    {
        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        private static readonly byte[] Minter = "AJedCGpz28puGEA75nUNZUCEechim7SHua".ToScriptHash(); // Dev: AJjGh9yY1MpbtBvD8rDDLKpyT3bizT3XA3

        private static readonly byte[] Owner = "AXZskPYG5JBM6nhpNZBMKZrxhHnyca1614".ToScriptHash(); // Dev: AHDfSLZANnJ4N9Rj3FCokP14jceu3u7Bvw

        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(Owner);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;

                if (method == "balanceOf") return BalanceOf((byte[])args[0]);

                if (method == "decimals") return Decimals();

                if (method == "name") return Name();

                if (method == "symbol") return Symbol();

                if (method == "totalSupply") return TotalSupply();

                if (method == "transfer") return Transfer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], callscript);
            }
            return false;
        }

        [DisplayName("balanceOf")]
        public static BigInteger BalanceOf(byte[] account)
        {
            if (account.Length != 20)
                throw new InvalidOperationException("The parameter account SHOULD be 20-byte addresses.");
            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            return asset.Get(account).TryToBigInteger();
        }

        [DisplayName("decimals")]
        public static byte Decimals() => 8;

        [DisplayName("name")]
        public static string Name() => "Switcheo";

        [DisplayName("symbol")]
        public static string Symbol() => "SWTH";

        [DisplayName("totalSupply")]
        public static BigInteger TotalSupply()
        {
            StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
            return contract.Get("totalSupply").TryToBigInteger();
        }

#if DEBUG
        [DisplayName("transfer")] //Only for ABI file
        public static bool Transfer(byte[] from, byte[] to, BigInteger amount) => true;
#endif
        //Methods of actual execution
        private static bool Transfer(byte[] from, byte[] to, BigInteger amount, byte[] callscript)
        {
            //Check parameters
            if (from.Length != 20 || to.Length != 20)
                throw new InvalidOperationException("The parameters from and to SHOULD be 20-byte addresses.");
            if (amount <= 0)
                throw new InvalidOperationException("The parameter amount MUST be greater than 0.");
            if (!Runtime.CheckWitness(from) && from.TryToBigInteger() != callscript.TryToBigInteger())
                return false;

            //Mint tokens first if minter is sender
            if (from.TryToBigInteger() == Minter.TryToBigInteger()) {
                if (from == to) {
                  throw new InvalidOperationException("Cannot mint to minter address.");
                }
                Mint(amount);
            }

            //Get payer balance
            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            var fromAmount = asset.Get(from).TryToBigInteger();

            //Check payer balance
            if (fromAmount < amount)
                return false;
            if (from == to)
                return true;

            //Reduce payer balances
            if (fromAmount == amount)
                asset.Delete(from);
            else
                asset.Put(from, fromAmount - amount);

            //Increase the payee balance
            var toAmount = asset.Get(to).TryToBigInteger();
            asset.Put(to, toAmount + amount);

            Transferred(from, to, amount);

            //Burn tokens if sending back to minter
            if (to.TryToBigInteger() == Minter.TryToBigInteger()) {
                Burn(amount);
            }
            return true;
        }

        private static void Mint(BigInteger amount) {
          StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
          StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));

          //Mint tokens to minter contract
          var minterAmount = asset.Get(Minter).TryToBigInteger();
          asset.Put(Minter, minterAmount + amount);

          //Update supply
          contract.Put("totalSupply", TotalSupply() + amount);

          //Signify mint by transfer from null address
          Transferred(null, Minter, amount);
        }

        private static void Burn(BigInteger amount) {
          StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
          StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));

          //Burn tokens from minter contract
          var minterAmount = asset.Get(Minter).TryToBigInteger();
          var newAmount = minterAmount - amount;
          if (newAmount < 0)
              throw new InvalidOperationException("Cannot burn more than in minter.");
          asset.Put(Minter, newAmount);

          //Update supply
          var newSupply = TotalSupply() - amount;
          if (newSupply < 0)
             throw new InvalidOperationException("Cannot burn more than current supply.");
          contract.Put("totalSupply", newSupply);

          //Signify burn by transfer to null address
          Transferred(Minter, null, amount);
        }
    }

    public static class Helper
    {
        public static BigInteger TryToBigInteger(this byte[] value)
        {
            return value?.ToBigInteger() ?? 0;
        }
    }
}
