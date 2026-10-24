public interface IPitMenu
{
    bool Connect();
    void Disconnect();
    bool switchMFD(string display = "MFDB");
    bool startUsingPitMenu();
    void setDelay(int mS, int initialDelay);
    bool PitRequest();
    string GetCategory(bool log = false);
    string CategoryUp();
    string CategoryDown();
    string ChoiceInc();
    string ChoiceDec();
    string GetChoice();
    bool SetCategory(string category);
    bool SoftMatchCategory(string category);
    bool SetChoice(string choice);
}