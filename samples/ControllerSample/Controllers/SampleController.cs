using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Dapr;

namespace ControllerSample.Controllers
{
    [ApiController]
    public class SampleController : ControllerBase
    {
        [HttpGet("{account}")]
        public ActionResult<Account> Get(StateEntry<Account> account)
        {
            if (account.Value is null)
            {
                return NotFound();
            }

            return account.Value;
        }

        [Topic("deposit")]
        [HttpPost("deposit")]
        public async Task<ActionResult<Account>> Deposit(Transaction transaction, [FromServices] StateClient stateClient)
        {
            var state = await stateClient.GetStateEntryAsync<Account>(transaction.Id);
            state.Value.Balance += transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }

        [Topic("withdraw")]
        [HttpPost("withdraw")]
        public async Task<ActionResult<Account>> Withdraw(Transaction transaction, [FromServices] StateClient stateClient)
        {
            var state = await stateClient.GetStateEntryAsync<Account>(transaction.Id);
            state.Value.Balance -= transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }
    }
}
