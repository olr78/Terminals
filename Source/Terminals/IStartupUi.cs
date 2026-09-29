using Terminals.Data;

namespace Terminals
{
    public interface IStartupUi
    {
        bool UserWantsFallback();

        AuthenticationPrompt KnowsUserPassword(bool previousTrySuccess);

        void Exit();

        /// <summary>
        /// Called after the user was authenticated, when the application starts to load the data.
        /// </summary>
        void ShowLoading();
    }
}