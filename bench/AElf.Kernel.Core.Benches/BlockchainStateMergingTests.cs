using System;
using System.Diagnostics;
using AElf.BenchBase;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Kernel.TransactionPool.Infrastructure;
using AElf.OS;
using NBench;
using Pro.NBench.xUnit.XunitExtensions;
using Volo.Abp.Threading;
using Xunit.Abstractions;

namespace AElf.Kernel.Core.Benches
{
    public class BlockchainStateMergingTests: BenchBaseTest<KernelCoreBenchAElfModule>
    {
        private IBlockchainStateMergingService _blockchainStateMergingService;
        private IBlockchainService _blockchainService;
        private IChainManager _chainManager;
        private OSTestHelper _osTestHelper;
        
        private Counter _counter;
        
        public BlockchainStateMergingTests(ITestOutputHelper output)
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new XunitTraceListener(output));
        }

        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            _blockchainStateMergingService = GetRequiredService<IBlockchainStateMergingService>();
            _blockchainService = GetRequiredService<IBlockchainService>();
            _osTestHelper = GetRequiredService<OSTestHelper>();
            _chainManager = GetRequiredService<IChainManager>();
                
            _counter = context.GetCounter("TestCounter");

            AsyncHelper.RunSync(async () =>
            {
                var transactions = await _osTestHelper.GenerateTransferTransactions(1000);
                await _osTestHelper.BroadcastTransactions(transactions);
                await _osTestHelper.MinedOneBlock();

                var chain = await _blockchainService.GetChainAsync();
                await _chainManager.SetIrreversibleBlockAsync(chain, chain.BestChainHash);
            });
        }

        [NBenchFact]
        [PerfBenchmark(NumberOfIterations = 5, RunMode = RunMode.Iterations,
            RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
        [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, .0d)]
        public void MergeBlockStateTest()
        {
            AsyncHelper.RunSync(async () =>
            {
                var chain = await _blockchainService.GetChainAsync();
                await _blockchainStateMergingService.MergeBlockStateAsync(chain.BestChainHeight, chain.BestChainHash);
            });
            _counter.Increment();
        }
    }
}