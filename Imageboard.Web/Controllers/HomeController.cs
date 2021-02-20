﻿using Imageboard.Data.Contexts;
using Imageboard.Data.Enteties;
using Imageboard.Data.Enums;
using Imageboard.Web.Models.ViewModels;
using Imageboard.Services.Markup;
using Imageboard.Services.ImageHandling;
using Imageboard.Tests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Imageboard.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IParser _parser;
        private readonly IImageHandler _imageHandler;
        private readonly IWebHostEnvironment _appEnvironment;
        
        public HomeController(ApplicationDbContext context, IParser parser, IImageHandler imageHandler, IWebHostEnvironment appEnvironment)
        {
            _db = context;
            _parser = parser;
            _imageHandler = imageHandler;
            _appEnvironment = appEnvironment;

            if (!_db.Boards.Any())
            {
                _db.Boards.Add(TestDataGenerator.GenerateData());
                _db.SaveChanges();
            }
        }

        [HttpPost]
        public IActionResult Delete(Dictionary<int, int> ids, int boardId)
        {
            var posts = _db.Posts.Where(p => ids.Values.Contains(p.Id));
            var openingPosts = posts.Where(p => p.IsOp);

            DeleteTreads(openingPosts);
            DeletePosts(posts.Except(openingPosts));
            _db.SaveChanges();

            return RedirectToAction("DisplayBoard", new { id = boardId });
        }

        [NonAction]
        public void DeletePosts(IEnumerable<Post> posts)
        {
            _db.Posts.RemoveRange(posts);
        }

        [NonAction]
        public void DeleteTreads(IEnumerable<Post> openingPosts)
        {
            var treadIds = openingPosts.Select(p => p.TreadId);
            var treads = _db.Treads.Where(t => treadIds.Contains(t.Id));

            _db.Treads.RemoveRange(treads);
        }

        [HttpPost]
        public IActionResult ReplyToTread(string message, string title, bool isSage, int treadId, IFormFile file, Destination dest)
        {
            var tread = _db.Treads.Single(t => t.Id == treadId);
            _db.Entry(tread).Collection(t => t.Posts).Load();

            Image img = _imageHandler.HandleImage(file, _appEnvironment.WebRootPath);
            tread.Posts.Add(new Post(_parser.ToHtml(message, _db), title, DateTime.Now, img, false, isSage, tread));

            _db.Update(tread);
            _db.SaveChanges();

            if (dest == Destination.Tread) return RedirectToAction("DisplayTread", new { id = treadId });
            else return RedirectToAction("DisplayBoard", new { id = tread.BoardId });
        }

        public IActionResult StartNewTread(string message, string title, bool isSage, int boardId, IFormFile file, Destination dest)
        {
            var board = _db.Boards.Single(t => t.Id == boardId);
            _db.Entry(board).Collection(b => b.Treads).Load();

            Image img = _imageHandler.HandleImage(file, _appEnvironment.WebRootPath);
            var oPost = new Post(_parser.ToHtml(message, _db), title, DateTime.Now, img, true, isSage);
            var tread = new Tread(board, oPost);

            board.Treads.Add(tread);

            _db.Update(board);
            _db.SaveChanges();

            if (dest == Destination.Board) return RedirectToAction("DisplayBoard", new { id = boardId });
            else return RedirectToAction("DisplayTread", new { id = tread.Id });
        }

        [HttpGet]
        public IActionResult DisplayBoard(int id = 1)
        {
            var board = _db.Boards.Single(b => b.Id == id);

            _db.Entry(board).Collection(b => b.Treads).Load();

            foreach (var tread in board.Treads)
            {
                _db.Entry(tread).Collection(t => t.Posts).Load();
                foreach (var post in tread.Posts) _db.Entry(post).Reference(p => p.Image).Load();
                tread.Posts = tread.Posts.OrderBy(p => p.Time).ToList();
            }

            board.Treads = board.Treads.OrderByDescending(t => t.Posts.LastOrDefault(p => !p.IsSage)?.Time ?? t.Posts.First().Time).ToList();

            return View(new BoardViewModel(board));
        }

        [HttpGet]
        public IActionResult DisplayTread(int id)
        {
            var tread = _db.Treads.Single(t => t.Id == id);

            _db.Entry(tread).Collection(t => t.Posts).Load();
            _db.Entry(tread).Reference(t => t.Board).Load();
            foreach (var post in tread.Posts) _db.Entry(post).Reference(p => p.Image).Load();

            tread.Posts = tread.Posts.OrderBy(p => p.Time).ToList();
            return View(new TreadViewModel(tread));
        }
    }
}
