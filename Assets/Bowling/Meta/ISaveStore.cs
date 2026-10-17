// Where SaveSystem's JSON actually lives. FileSaveStore is the real one; tests use a fake.
public interface ISaveStore
{
    string Load();
    void Write(string json);
}
