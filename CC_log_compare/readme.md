Tool which zeros the start time in a log file (and removes the seconds decimal) to make it easier to compare log files.
Writes it to _input file_.timeZeroed

Also parses the fuel content of a log file into a CSV file.  Writes it to _input file_.fuel.CSV

Of course, we could just change the logging code but I presume there's some reason why it includes time of day and milliseconds.