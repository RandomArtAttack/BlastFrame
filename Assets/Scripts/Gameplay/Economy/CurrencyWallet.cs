using System;

namespace BlastFrame.Gameplay.Economy
{
    /// <summary>
    /// Plain struct wrapping the meta-currency balance. Used internally by CurrencyManager
    /// to keep mutation logic in one place. Not a MonoBehaviour — no Unity lifecycle.
    /// </summary>
    public struct CurrencyWallet
    {
        private int _balance;

        /// <summary>Current meta-currency balance. Always >= 0.</summary>
        public int Balance => _balance;

        public CurrencyWallet(int initialBalance)
        {
            _balance = Math.Max(0, initialBalance);
        }

        /// <summary>Add a positive amount to the balance. Negative amounts are ignored.</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            _balance += amount;
        }

        /// <summary>
        /// Deduct <paramref name="amount"/> if the balance is sufficient.
        /// Returns true and deducts on success; returns false and leaves balance unchanged on failure.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || _balance < amount) return false;
            _balance -= amount;
            return true;
        }

        /// <summary>Replace the entire balance (used when loading save data).</summary>
        public void SetBalance(int balance) => _balance = Math.Max(0, balance);
    }
}
