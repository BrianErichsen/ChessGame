//Author: Brian Erichsen Fagundes
//CS6016 - Summer - 2024
//Database Systems and Applications
//Professor: Nabil Makarem

using AuthenticationServices;
using CoreMedia;
using GameKit;
using Microsoft.Maui.Controls;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChessBrowser
{
  // Represents one instance of a chess game, such as players, round
  // result, moves, event name and date ...
  internal class ChessGame {
    //provide public get and set methods, through properties, to access
    //and update the value of a private field
    public string Event { get; set;}
    public string Site { get; set;}
    public DateTime EventDate { get; set;}
    public string Round { get; set;}
    public string White { get; set;}
    public string Black { get; set;}
    public int WhiteElo { get; set;}
    public int BlackElo { get; set;}
    public char Result { get; set;}
    public string Moves { get; set;}
  }//end of class ChessGame

  //Class that provides methods to return list of chessgame given
  //path to PGN file
  static class PgnReader {
    //this is how a list of chessgames object is created based on file given as argument
    public static List<ChessGame> ReadPgnFile(string filePath) {
      List<ChessGame> games = new List<ChessGame>();
      //make sure that file is in correct directory
      //System.Diagnostics.Debug.WriteLine($"String filepath is: {filePath}");
      string fileLines = File.ReadAllText(filePath);
      //each current game is a list of strings split double empty lines
      string[] currentGame = Regex.Split(fileLines, @"\r?\n\r?\n");

      //this is how we partition each game
      for (int i = 0; i < currentGame.Length; i += 2) {
        string line = currentGame[i];
        string moves_ = i+1 < currentGame.Length ? currentGame[i + 1] : "";

        ChessGame game = ParseGame(line);
        if (game != null) {
          game.Moves = moves_;
          games.Add(game);
        }
      }
      return games;
    }//end of helper method read PGN file

    //helper method Parsegame
    private static ChessGame ParseGame(string line) {
      ChessGame game = null;
      // \[ matches '[' char, w+ captures 1 or + words char, \s matches any whitespace
      //'""' matches double quote adding + for a group
      MatchCollection tag = Regex.Matches(line, @"\[(\w+)\s""([^""]+)""\]");
      if (tag.Count >= 7) {
        game = new ChessGame();
        string event_str = ExtractTagValue(tag, "Event");
        //filters out illegal characters
        //\p{L} matches  not (^) unicode letter, number, punctuation, separator
        event_str = Regex.Replace(event_str, @"[^\p{L}\p{N}\p{P}\p{Z}]", "");
        game.Event = event_str;
        game.Site = ExtractTagValue(tag, "Site");
        string eventDate_str = ExtractTagValue(tag, "EventDate");
        DateTime eventDate_;
        //culture info - culture independent object
        if (!DateTime.TryParseExact(eventDate_str, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out eventDate_)) {
          eventDate_ = DateTime.MinValue;
        }
        game.EventDate = eventDate_;
        game.Round = ExtractTagValue(tag, "Round");
        game.White = ExtractTagValue(tag, "White");
        game.Black = ExtractTagValue(tag, "Black");
        game.WhiteElo = int.Parse(ExtractTagValue(tag, "WhiteElo"));
        game.BlackElo = int.Parse(ExtractTagValue(tag, "BlackElo"));
        game.Result = ParseResult(ExtractTagValue(tag, "Result"));
        //empty at first;
        game.Moves = "";
      }
      return game;
    }//end of parseGame helper method

    //Extracts the value of pgn line
    //matches any text within "" and captures inner content
    private static string ExtractTagValue(MatchCollection matches ,string line) {
      foreach (Match match in matches) {
        if (match.Groups[1].Value == line) {
          return match.Groups[2].Value;
        }
      }
      return null;
    }//end of Extract Tag Value helper method
    //converts date from file to date object

    //converts string to char denoting which player won
    private static char ParseResult(string result) {
      //if -- then return char -->
      return result switch {
        "1-0" => 'W',
        "0-1" => 'B',
        "1/2-1/2" => 'D',
        _ => 'D'
      };
    }
  }//end of class PgnReader
  internal class Queries
  {
    /// <summary>
    /// This function runs when the upload button is pressed.
    /// Given a filename, parses the PGN file, and uploads
    /// each chess game to the user's database.
    /// </summary>
    /// <param name="PGNfilename">The path to the PGN file</param>
    internal static async Task InsertGameData( string PGNfilename, MainPage mainPage )
    {
      // This will build a connection string to your user's database on atr,
      // assuimg you've typed a user and password in the GUI
      string connection = mainPage.GetConnectionString();

      List<ChessGame> games = PgnReader.ReadPgnFile(PGNfilename);
      mainPage.SetNumWorkItems(games.Count);


      using ( MySqlConnection conn = new MySqlConnection( connection ) )
      {
        try
        {
          // Open a connection
          conn.Open();

          foreach(var game in games)
          {
            await InsertEvent(conn, game);
            await InsertPlayer(conn, game.White, game.WhiteElo);
            await InsertPlayer(conn, game.Black, game.BlackElo);
            await InsertGame(conn, game);
            await mainPage.NotifyWorkItemCompleted();
          }
        }
        catch ( Exception e )
        {
          System.Diagnostics.Debug.WriteLine( e.Message );
          System.Diagnostics.Debug.WriteLine(e.StackTrace);
        }
      }
      //inserts data into the Events table
      static async Task InsertEvent(MySqlConnection conn, ChessGame game) {
        var cmd = new MySqlCommand("INSERT IGNORE INTO Events (Name, Site, Date) VALUES (@Name, @Site, @Date) ON DUPLICATE KEY UPDATE Name = Name", conn);
        //uses the class member variables to extract proper values
        cmd.Parameters.AddWithValue("@Name", game.Event);
        cmd.Parameters.AddWithValue("@Site", game.Site);
        cmd.Parameters.AddWithValue("@Date", game.EventDate.Date.ToString("yyyy-MM-dd"));
        await cmd.ExecuteNonQueryAsync();
      } //in this block it stops // error processing variable
      static async Task InsertPlayer(MySqlConnection conn, string playerName, int elo) {
        var cmd = new MySqlCommand("INSERT INTO Players (Name, Elo) VALUES (@Name, @Elo) ON DUPLICATE KEY UPDATE Elo = GREATEST(Elo, @Elo)", conn);
        //uses the class member variables to extract propoer values
        cmd.Parameters.AddWithValue("@Name", playerName);
        cmd.Parameters.AddWithValue("@Elo", elo);
        await cmd.ExecuteNonQueryAsync();
      }
      static async Task InsertGame(MySqlConnection conn, ChessGame game) {
        //gets specific eID
      
        var getEventId = new MySqlCommand("SELECT eID FROM Events WHERE Name = @Name AND Site = @Site AND Date = @Date", conn);
        getEventId.Parameters.AddWithValue("@Name", game.Event);
        getEventId.Parameters.AddWithValue("Site", game.Site);
        getEventId.Parameters.AddWithValue("@Date", game.EventDate);
        //stores eventID value into variable
        var eventID = Convert.ToUInt64(getEventId.ExecuteScalar());

        //gets specific Players ID
        var getPlayerId = new MySqlCommand("SELECT pID FROM Players WHERE Name = @Name", conn);
        getPlayerId.Parameters.AddWithValue("@Name", game.White);
        //stores result of query into whitePlayerID
        var whitePlayerId = Convert.ToUInt64(getPlayerId.ExecuteScalar());
        //does the query again but now with game.Black name's instead
        getPlayerId.Parameters["@Name"].Value = game.Black;
        //stores new query result into blackPlayerID
        var blackPlayerId = Convert.ToUInt64(getPlayerId.ExecuteScalar());

        using (MySqlCommand cmd = new MySqlCommand("INSERT IGNORE INTO Games (Round, Result, Moves, BlackPlayer, WhitePlayer, eID) VALUES (@Round, @Result, @Moves, @BlackPlayer, @WhitePlayer, @eID)", conn)) {
          cmd.Parameters.AddWithValue("@Round", game.Round);
          cmd.Parameters.AddWithValue("@Result", game.Result);
          cmd.Parameters.AddWithValue("@Moves", game.Moves);
          cmd.Parameters.AddWithValue("@BlackPlayer", blackPlayerId);
          cmd.Parameters.AddWithValue("@WhitePlayer", whitePlayerId);
          cmd.Parameters.AddWithValue("@eID", eventID);
          await cmd.ExecuteNonQueryAsync();
        }
      }//end of inner insert game
    }//end of insterGameData method

    /// <summary>
    /// Queries the database for games that match all the given filters.
    /// The filters are taken from the various controls in the GUI.
    /// </summary>
    /// <param name="white">The white player, or null if none</param>
    /// <param name="black">The black player, or null if none</param>
    /// <param name="opening">The first move, e.g. "1.e4", or null if none</param>
    /// <param name="winner">The winner as "W", "B", "D", or null if none</param>
    /// <param name="useDate">True if the filter includes a date range, False otherwise</param>
    /// <param name="start">The start of the date range</param>
    /// <param name="end">The end of the date range</param>
    /// <param name="showMoves">True if the returned data should include the PGN moves</param>
    /// <returns>A string separated by newlines containing the filtered games</returns>
    internal static string PerformQuery( string white, string black, string opening,
      string winner, bool useDate, DateTime start, DateTime end, bool showMoves,
      MainPage mainPage )
    {
      // This will build a connection string to your user's database on atr,
      // assuimg you've typed a user and password in the GUI
      string connection = mainPage.GetConnectionString();
      //string connection = "server=cs-db.eng.utah.edu; database = u6016368; uid = u6016368; password = changeThisPassword";

      // Build up this string containing the results from your query
      StringBuilder parsedResult = new StringBuilder();

      // Use this to count the number of rows returned by your query
      // (see below return statement)
      int numRows = 0;

      using ( MySqlConnection conn = new MySqlConnection( connection ) )
      {
        try
        {
          // Open a connection
          conn.Open();
          //       Generate and execute an SQL command,
          //Construct SQL Query from Events details, games, and Player
          //creates dynamic string to be the query
          //Uses eName to avoid aliases
          //pName as white player
          //bName as name of black player
          StringBuilder query = new StringBuilder("SELECT Events.Name as eName, Events.Site, Events.Date, Games.Round, Games.Result, WhitePlayer.Name as pName, WhitePlayer.Elo, ");
          //if PNG moves are involved then append games moves
          if (showMoves) {
            query.Append("Games.Moves, ");
          }
          //joins Games table with Events and Players
          //retrieves values with matching values of != tables
          query.Append("BlackPlayer.Name as bName, BlackPlayer.Elo as bElo ");
          query.Append("FROM Games ");
          query.Append("INNER JOIN Events ON Games.eID = Events.eID ");
          query.Append("INNER JOIN Players as WhitePlayer ON Games.WhitePlayer = WhitePlayer.pID ");
          query.Append("INNER JOIN Players as BlackPlayer ON Games.BlackPlayer = BlackPlayer.pID ");
          query.Append("WHERE 1=1 ");//base condition where we have a valid condition

          //filters the query
          if (!string.IsNullOrEmpty(white))
          {
            query.Append("AND WhitePlayer.Name = @WhitePlayer ");
          }
          if (!string.IsNullOrEmpty(black))
            query.Append("AND BlackPlayer.Name = @BlackPlayer ");
          if (!string.IsNullOrEmpty(opening))
            query.Append("AND Games.Moves LIKE @Opening ");
          if (!string.IsNullOrEmpty(winner))
            query.Append("AND Games.Result = @Winner ");
          if (useDate)
            query.Append("AND Events.Date BETWEEN @StartDate AND @EndDate ");
          System.Diagnostics.Debug.WriteLine(query.ToString());

          using (var cmd = new MySqlCommand(query.ToString(), conn))
          {
            //Add Parementers to prevent SQL Injection
            if (!string.IsNullOrEmpty(white))
              cmd.Parameters.AddWithValue("@WhitePlayer", white);
            if (!string.IsNullOrEmpty(black))
              cmd.Parameters.AddWithValue("@BlackPlayer", black);
            if (!string.IsNullOrEmpty(opening))
              cmd.Parameters.AddWithValue("@Opening", opening + "%");
            if (!string.IsNullOrEmpty(winner))
              cmd.Parameters.AddWithValue("@Winner", winner);
            if (useDate)
            {
              cmd.Parameters.AddWithValue("@StartDate", start);
              cmd.Parameters.AddWithValue("@EndDate", end);
            }
            using (var reader = cmd.ExecuteReader())
            {
              while (reader.Read())
              {
                numRows++;
                parsedResult.AppendLine($"Event: {reader["eName"]}");
                parsedResult.AppendLine($"Site: {reader["Site"]}");
                parsedResult.AppendLine($"Date: {reader.GetDateTime("Date"):yyyy-MM-dd}");
                parsedResult.AppendLine($"Round: {reader["Round"]}");
                parsedResult.AppendLine($"White: {reader["pName"]} ({reader["Elo"]})");
                parsedResult.AppendLine($"Black: {reader["bName"]} ({reader["bElo"]})");
                parsedResult.AppendLine($"Result: {reader["Result"]}");
                if (showMoves)
                  parsedResult.AppendLine($"Moves: {reader["Moves"]}");
                parsedResult.AppendLine(); // newline between results
              }
            }
          }
        }
        catch ( Exception e )
        {
          System.Diagnostics.Debug.WriteLine( e.Message );
        }
      }
      //converts back result from string builder to string
      return numRows + " results\n" + parsedResult.ToString();
    }//end of perform query method
  }//end of class queries
}//end of name space chess browser