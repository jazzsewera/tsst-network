﻿namespace CableCloud.Ui.Parsers
{
    public interface ICommandParser
    {
        (string, string, bool) ParseCommand(string command);
    }
}
